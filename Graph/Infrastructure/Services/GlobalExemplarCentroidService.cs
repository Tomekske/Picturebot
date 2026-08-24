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

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Query pictures and their metrics having embeddings
        var picturesData = await context.Pictures
            .Include(p => p.Metrics)
            .Where(p => p.Metrics != null && p.Metrics.Embedding != null)
            .Select(p => new {
                p.Id,
                p.KeywordsJson,
                EmbeddingBytes = p.Metrics!.Embedding
            })
            .ToListAsync(cancellationToken);

        foreach (var p in picturesData) {
            if (p.EmbeddingBytes == null || p.EmbeddingBytes.Length != 512 * sizeof(float)) {
                continue;
            }

            var vector = new float[512];
            Buffer.BlockCopy(p.EmbeddingBytes, 0, vector, 0, p.EmbeddingBytes.Length);

            var keywords = ParseKeywords(p.KeywordsJson);
            foreach (var kw in keywords) {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                var leafTag = ExtractLeafTag(kw);
                if (string.IsNullOrEmpty(leafTag)) continue;

                var pictureMap = _exemplarCache.GetOrAdd(leafTag, _ => new ConcurrentDictionary<int, float[]>());
                pictureMap[p.Id] = vector;
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
