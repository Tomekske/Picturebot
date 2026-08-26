using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Graph.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Graph.Infrastructure.Services;

public class GlobalExemplarCentroidService : IGlobalExemplarCentroidService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, float[]>> _exemplarCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, float[]> _centroidCache = new(StringComparer.OrdinalIgnoreCase);

    public int MinimumExemplarThreshold { get; set; } = 10;

    public GlobalExemplarCentroidService(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
    }

    public async Task<Dictionary<string, float[]>> GetActiveLeafCentroidsAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        Serilog.Log.Information("Few-Shot Tag Discovery: Querying tagged pictures from database...");
        List<Picture> picturesData;
        using (var scope = _scopeFactory.CreateScope()) {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            picturesData = await context.Pictures
                .AsNoTracking()
                .Include(p => p.Metrics)
                .Include(p => p.Parent)
                .Where(p => p.KeywordsJson != null && p.KeywordsJson != "" && p.KeywordsJson != "[]")
                .ToListAsync(cancellationToken);
        }

        Serilog.Log.Information("Few-Shot Tag Discovery: Loaded {Count} tagged picture records from database.", picturesData.Count);

        using (var scope = _scopeFactory.CreateScope()) {
            var embeddingService = scope.ServiceProvider.GetService<PictureWorker.Domain.Interfaces.IImageEmbeddingService>();
            var pathService = scope.ServiceProvider.GetService<Domain.Interfaces.IPathService>();

            int exemplarIndex = 0;
            int totalExemplars = picturesData.Count;

            foreach (var p in picturesData) {
                cancellationToken.ThrowIfCancellationRequested();
                exemplarIndex++;
                if (exemplarIndex % 10 == 0 || exemplarIndex == totalExemplars || exemplarIndex == 1) {
                    Serilog.Log.Information("CentroidService: Ingested exemplar {Index}/{Total} ({Name})...", exemplarIndex, totalExemplars, p.Name);
                }

                if (p.Parent != null && pathService != null) {
                    pathService.PopulatePaths(p);
                }

                var keywords = ParseKeywords(p.KeywordsJson);
                if (keywords.Count == 0) continue;

                // Ensure embedding vector is computed and loaded
                float[]? vector = null;
                if (p.Metrics?.Embedding != null && p.Metrics.Embedding.Length == 512 * sizeof(float)) {
                    vector = p.Metrics.GetEmbeddingVector();
                } else if (embeddingService != null) {
                    try {
                        vector = await embeddingService.GetOrComputeEmbeddingAsync(p, cancellationToken);
                    } catch { }
                }

                if (vector == null || vector.Length != 512) continue;

                foreach (var kw in keywords) {
                    if (string.IsNullOrWhiteSpace(kw)) continue;
                    var leafTag = ExtractLeafTag(kw);
                    if (string.IsNullOrEmpty(leafTag)) continue;

                    var pictureMap = _exemplarCache.GetOrAdd(leafTag, _ => new ConcurrentDictionary<int, float[]>());
                    pictureMap[p.Id] = vector;
                }
            }
        }

        RecomputeAllCentroids();

        var result = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _centroidCache) {
                if (_exemplarCache.TryGetValue(kvp.Key, out var map) && map.Count >= MinimumExemplarThreshold) {
                    result[kvp.Key] = kvp.Value;
                }
            }

        Serilog.Log.Information("Few-Shot Tag Discovery: Found {Count} active leaf centroids (threshold N={Threshold}): [{Tags}]",
            result.Count, MinimumExemplarThreshold, string.Join(", ", result.Keys));

        return result;
    }

    public void OnTagAdded(int pictureId, string tag, float[] embedding) {
        if (string.IsNullOrWhiteSpace(tag) || embedding == null || embedding.Length != 512) {
            return;
        }

        var leafTag = ExtractLeafTag(tag);
        if (string.IsNullOrEmpty(leafTag)) return;

        var pictureMap = _exemplarCache.GetOrAdd(leafTag, _ => new ConcurrentDictionary<int, float[]>());
        pictureMap[pictureId] = embedding;

        RecomputeCentroidForTag(leafTag);
    }

    public void OnTagRemoved(int pictureId, string tag, float[] embedding) {
        if (string.IsNullOrWhiteSpace(tag)) {
            return;
        }

        var leafTag = ExtractLeafTag(tag);
        if (string.IsNullOrEmpty(leafTag)) return;

        if (_exemplarCache.TryGetValue(leafTag, out var pictureMap)) {
            pictureMap.TryRemove(pictureId, out _);
            RecomputeCentroidForTag(leafTag);
        }
    }

    private void RecomputeAllCentroids() {
        _centroidCache.Clear();
        foreach (var kvp in _exemplarCache) {
            RecomputeCentroidForTag(kvp.Key);
        }
    }

    private void RecomputeCentroidForTag(string tag) {
        if (!_exemplarCache.TryGetValue(tag, out var map) || map.Count < MinimumExemplarThreshold) {
            _centroidCache.TryRemove(tag, out _);
            return;
        }

        var sum = new float[512];
        foreach (var vec in map.Values) {
            for (int i = 0; i < 512; i++) {
                sum[i] += vec[i];
            }
        }

        double sumSq = 0.0;
        for (int i = 0; i < 512; i++) {
            sumSq += sum[i] * sum[i];
        }

        float norm = (float)Math.Sqrt(sumSq);
        if (norm < 1e-9f) {
            _centroidCache.TryRemove(tag, out _);
            return;
        }

        var normalized = new float[512];
        for (int i = 0; i < 512; i++) {
            normalized[i] = sum[i] / norm;
        }

        _centroidCache[tag] = normalized;
    }

    private static List<string> ParseKeywords(string? keywordsJson) {
        if (string.IsNullOrWhiteSpace(keywordsJson)) {
            return new List<string>();
        }

        try {
            return JsonSerializer.Deserialize<List<string>>(keywordsJson) ?? new List<string>();
        } catch {
            return new List<string>();
        }
    }

    private static string ExtractLeafTag(string tag) {
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
        var parts = tag.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1].Trim() : tag.Trim();
    }
}
