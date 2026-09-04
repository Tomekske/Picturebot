using System.Collections.Concurrent;
using System.Text.Json;
using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PictureWorker.Domain.Interfaces;
using Serilog;

namespace Graph.Infrastructure.Services;

public class GlobalExemplarCentroidService : IGlobalExemplarCentroidService {
    private readonly ConcurrentDictionary<string, float[]> _centroidCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, float[]>> _exemplarCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IServiceScopeFactory _scopeFactory;

    public GlobalExemplarCentroidService(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
    }

    public int MinimumExemplarThreshold { get; set; } = 10;

    public async Task<Dictionary<string, float[]>> GetActiveLeafCentroidsAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        Log.Information("Few-Shot Tag Discovery: Querying tagged pictures from database...");
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

        Log.Information("Few-Shot Tag Discovery: Loaded {Count} tagged picture records from database.",
            picturesData.Count);

        using (var scope = _scopeFactory.CreateScope()) {
            var embeddingService = scope.ServiceProvider.GetService<IImageEmbeddingService>();
            var pathService = scope.ServiceProvider.GetService<IPathService>();
            var settingsService = scope.ServiceProvider.GetService<ISettingsService>();

            var settings = settingsService?.Current;
            if (settings == null) {
                var settingsRepo = scope.ServiceProvider.GetService<ISettingsRepository>();
                if (settingsRepo != null) {
                    settings = await settingsRepo.LoadAsync();
                }
            }

            var excludedTagNames = GetExcludedTagNames(settings);
            var excludedPicturesCount = 0;

            var exemplarIndex = 0;
            var totalExemplars = picturesData.Count;

            foreach (var p in picturesData) {
                cancellationToken.ThrowIfCancellationRequested();
                exemplarIndex++;
                if (exemplarIndex % 10 == 0 || exemplarIndex == totalExemplars || exemplarIndex == 1) {
                    Log.Information("CentroidService: Ingested exemplar {Index}/{Total} ({Name})...", exemplarIndex,
                        totalExemplars, p.Name);
                }

                if (p.Parent != null && pathService != null) {
                    pathService.PopulatePaths(p);
                }

                var keywords = ParseKeywords(p.KeywordsJson);
                if (keywords.Count == 0) {
                    continue;
                }

                // Exclude pictures containing tags from excluded workflow tag groups
                if (excludedTagNames.Count > 0 && ContainsExcludedTag(keywords, excludedTagNames)) {
                    excludedPicturesCount++;
                    continue;
                }

                // Ensure embedding vector is computed and loaded
                float[]? vector = null;
                if (p.Metrics?.Embedding != null && p.Metrics.Embedding.Length == 512 * sizeof(float)) {
                    vector = p.Metrics.GetEmbeddingVector();
                } else if (embeddingService != null) {
                    try {
                        vector = await embeddingService.GetOrComputeEmbeddingAsync(p, cancellationToken);
                    } catch {
                    }
                }

                if (vector == null || vector.Length != 512) {
                    continue;
                }

                foreach (var kw in keywords) {
                    if (string.IsNullOrWhiteSpace(kw)) {
                        continue;
                    }

                    var leafTag = ExtractLeafTag(kw);
                    if (string.IsNullOrEmpty(leafTag)) {
                        continue;
                    }

                    var pictureMap = _exemplarCache.GetOrAdd(leafTag, _ => new ConcurrentDictionary<int, float[]>());
                    pictureMap[p.Id] = vector;
                }
            }

            if (excludedPicturesCount > 0) {
                Log.Information(
                    "CentroidService: Excluded {Count} image(s) from training exemplars due to excluded workflow tag groups.",
                    excludedPicturesCount);
            }
        }

        RecomputeAllCentroids();

        var result = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _centroidCache) {
            if (_exemplarCache.TryGetValue(kvp.Key, out var map) && map.Count >= MinimumExemplarThreshold) {
                result[kvp.Key] = kvp.Value;
            }
        }

        Log.Information(
            "Few-Shot Tag Discovery: Found {Count} active leaf centroids (threshold N={Threshold}): [{Tags}]",
            result.Count, MinimumExemplarThreshold, string.Join(", ", result.Keys));

        return result;
    }

    public void OnTagAdded(int pictureId, string tag, float[] embedding) {
        if (string.IsNullOrWhiteSpace(tag) || embedding == null || embedding.Length != 512) {
            return;
        }

        var leafTag = ExtractLeafTag(tag);
        if (string.IsNullOrEmpty(leafTag)) {
            return;
        }

        var pictureMap = _exemplarCache.GetOrAdd(leafTag, _ => new ConcurrentDictionary<int, float[]>());
        pictureMap[pictureId] = embedding;

        RecomputeCentroidForTag(leafTag);
    }

    public void OnTagRemoved(int pictureId, string tag, float[] embedding) {
        if (string.IsNullOrWhiteSpace(tag)) {
            return;
        }

        var leafTag = ExtractLeafTag(tag);
        if (string.IsNullOrEmpty(leafTag)) {
            return;
        }

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
            for (var i = 0; i < 512; i++) {
                sum[i] += vec[i];
            }
        }

        var sumSq = 0.0;
        for (var i = 0; i < 512; i++) {
            sumSq += sum[i] * sum[i];
        }

        var norm = (float)Math.Sqrt(sumSq);
        if (norm < 1e-9f) {
            _centroidCache.TryRemove(tag, out _);
            return;
        }

        var normalized = new float[512];
        for (var i = 0; i < 512; i++) {
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

    public static HashSet<string> GetExcludedTagNames(SettingsModel? settings) {
        var excludedTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (settings == null) {
            return excludedTagNames;
        }

        var excludedGroupTagIds = settings.TagGroups
            .Where(g => g.ExcludeFromTraining)
            .SelectMany(g => g.TagIds)
            .ToHashSet();

        if (excludedGroupTagIds.Count == 0) {
            return excludedTagNames;
        }

        foreach (var tag in settings.MasterTags) {
            if (excludedGroupTagIds.Contains(tag.Id) && !string.IsNullOrWhiteSpace(tag.Name)) {
                excludedTagNames.Add(tag.Name.Trim());
            }
        }

        return excludedTagNames;
    }

    public static bool ContainsExcludedTag(IEnumerable<string> keywords, HashSet<string> excludedTagNames) {
        if (excludedTagNames == null || excludedTagNames.Count == 0) {
            return false;
        }

        foreach (var kw in keywords) {
            if (string.IsNullOrWhiteSpace(kw)) {
                continue;
            }

            var trimmed = kw.Trim();
            if (excludedTagNames.Contains(trimmed)) {
                return true;
            }

            var leaf = ExtractLeafTag(trimmed);
            if (excludedTagNames.Contains(leaf)) {
                return true;
            }

            var normalized = trimmed.Replace(" › ", "|").Replace(" > ", "|").Replace('/', '|').Replace('\\', '|')
                .Trim('|');
            var segs = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segs.Any(s => excludedTagNames.Contains(s))) {
                return true;
            }
        }

        return false;
    }

    private static string ExtractLeafTag(string tag) {
        if (string.IsNullOrWhiteSpace(tag)) {
            return string.Empty;
        }

        var parts = tag.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1].Trim() : tag.Trim();
    }
}
