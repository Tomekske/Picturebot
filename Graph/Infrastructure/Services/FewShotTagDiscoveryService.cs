using System;
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
using PictureWorker.Domain.Interfaces;
using Serilog;

namespace Graph.Infrastructure.Services;

public class FewShotTagDiscoveryService : IFewShotTagDiscoveryService {
    private readonly IImageEmbeddingService _embeddingService;
    private readonly IGlobalExemplarCentroidService _centroidService;
    private readonly ITaxonomyService _taxonomyService;
    private readonly IXmpService _xmpService;
    private readonly IServiceScopeFactory _scopeFactory;

    public float SimilarityThreshold { get; set; } = 0.70f;

    public FewShotTagDiscoveryService(
        IImageEmbeddingService embeddingService,
        IGlobalExemplarCentroidService centroidService,
        ITaxonomyService taxonomyService,
        IXmpService xmpService,
        IServiceScopeFactory scopeFactory) {
        _embeddingService = embeddingService;
        _centroidService = centroidService;
        _taxonomyService = taxonomyService;
        _xmpService = xmpService;
        _scopeFactory = scopeFactory;
    }

    public async Task<List<TagDiscoveryResult>> ScanPicturesAsync(
        List<Picture> pictures,
        Action<Picture, List<string>>? onTagsDiscoveredOnUIThread = null,
        CancellationToken cancellationToken = default) {

        cancellationToken.ThrowIfCancellationRequested();

        if (pictures == null || pictures.Count == 0) {
            return new List<TagDiscoveryResult>();
        }

        // 1. Fetch active leaf centroids from global database
        var centroids = await _centroidService.GetActiveLeafCentroidsAsync(cancellationToken);
        if (centroids == null || centroids.Count == 0) {
            Log.Information("Few-Shot Tag Discovery: No active leaf centroids found (minimum threshold not reached for any tag). Skipping scan.");
            return new List<TagDiscoveryResult>();
        }

        Log.Information("Few-Shot Tag Discovery: Starting scan of {Count} picture(s) against {CentroidCount} active centroid(s): [{CentroidTags}]",
            pictures.Count, centroids.Count, string.Join(", ", centroids.Keys));

        var results = new System.Collections.Concurrent.ConcurrentBag<TagDiscoveryResult>();
        int maxParallelism = Math.Max(2, Math.Min(8, Environment.ProcessorCount));
        int processedCount = 0;
        int totalCount = pictures.Count;

        // 2. Process pictures in parallel
        await Parallel.ForEachAsync(pictures, new ParallelOptions {
            MaxDegreeOfParallelism = maxParallelism,
            CancellationToken = cancellationToken
        }, async (picture, ct) => {
            ct.ThrowIfCancellationRequested();

            int current = Interlocked.Increment(ref processedCount);
            Log.Information("Few-Shot Tag Discovery: Scanning picture {Current}/{Total} ({Name})...", current, totalCount, picture.Name);

            // Lock baseline committed tags (existing tags loaded from XMP sidecar)
            var committedTags = new HashSet<string>(picture.Keywords ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            // Ingest / load image embedding vector
            var embedding = await _embeddingService.GetOrComputeEmbeddingAsync(picture, ct);
            if (embedding == null || embedding.Length != 512) {
                return;
            }

            var discoveredLeafs = new List<string>();
            var newFlatTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newHierarchicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 3. Match against active leaf centroids
            var candidateScores = new List<(string LeafTag, float Similarity)>();
            foreach (var (leafTag, centroid) in centroids) {
                ct.ThrowIfCancellationRequested();

                // Check if leaf tag is already committed
                if (committedTags.Contains(leafTag) || IsLeafPresentInKeywords(committedTags, leafTag)) {
                    continue;
                }

                // Compute Cosine Similarity (both embedding and centroid are unit-normalized)
                float sim = ComputeCosineSimilarity(embedding, centroid);

                if (sim >= SimilarityThreshold) {
                    candidateScores.Add((leafTag, sim));
                }
            }

            if (candidateScores.Count == 0) {
                return;
            }

            // Select top-ranked candidate(s) within competitive margin
            float maxScore = candidateScores.Max(c => c.Similarity);
            var winningCandidates = candidateScores
                .Where(c => c.Similarity >= maxScore - 0.05f)
                .Select(c => c.LeafTag)
                .ToList();

            foreach (var leafTag in winningCandidates) {
                discoveredLeafs.Add(leafTag);

                // 4. Resolve Taxonomy Expansion
                var flatChain = _taxonomyService.ResolveTaxonomySubjectChain(leafTag);
                var fullHierarchy = _taxonomyService.GetFullHierarchicalPath(leafTag);

                foreach (var ft in flatChain) {
                    newFlatTags.Add(ft);
                }
                if (!string.IsNullOrEmpty(fullHierarchy)) {
                    newHierarchicalPaths.Add(fullHierarchy);
                }
            }

            // 5. Non-Destructive Tag Merging (Additive Set Union)
            var updatedKeywords = new HashSet<string>(picture.Keywords ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var flatTag in newFlatTags) {
                updatedKeywords.Add(flatTag);
            }
            foreach (var hierPath in newHierarchicalPaths) {
                updatedKeywords.Add(hierPath);
            }

            picture.Keywords = updatedKeywords.ToList();
            picture.KeywordsJson = JsonSerializer.Serialize(picture.Keywords);

            Log.Information("Few-Shot Tag Discovery: Auto-tagged picture '{PictureName}' (Id={PictureId}) with tags [{Tags}]",
                picture.Name, picture.Id, string.Join(", ", newFlatTags.Concat(newHierarchicalPaths)));

            // Trigger live UI thread callback if provided
            onTagsDiscoveredOnUIThread?.Invoke(picture, picture.Keywords);

            // 6. Asynchronous Non-Destructive XMP Auto-Save
            try {
                await _xmpService.SaveMetadataAsync(picture);
            } catch (Exception ex) {
                Log.Error(ex, "Failed auto-saving XMP sidecar after tag discovery for picture {Name}", picture.Name);
            }

            results.Add(new TagDiscoveryResult(
                picture,
                discoveredLeafs,
                newFlatTags.ToList(),
                newHierarchicalPaths.ToList()
            ));
        });

        var resultList = results.ToList();

        // 7. Single Batch SQLite Update for all discovered pictures
        if (resultList.Count > 0) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var changedPictureIds = resultList.Where(r => r.Picture.Id > 0).Select(r => r.Picture.Id).ToList();
                var dbPictures = await dbContext.Pictures
                    .Where(p => changedPictureIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);

                var resultMap = resultList.ToDictionary(r => r.Picture.Id, r => r.Picture.KeywordsJson);
                foreach (var dbPic in dbPictures) {
                    if (resultMap.TryGetValue(dbPic.Id, out var kwJson)) {
                        dbPic.KeywordsJson = kwJson;
                    }
                }
                await dbContext.SaveChangesAsync(cancellationToken);
            } catch (Exception ex) {
                Log.Warning(ex, "Failed batch persisting updated KeywordsJson to SQLite");
            }
        }

        Log.Information("Few-Shot Tag Discovery: Completed scan of {PictureCount} pictures. Found {ResultCount} pictures with newly discovered tags.",
            pictures.Count, resultList.Count);

        return resultList;
    }

    private static float ComputeCosineSimilarity(float[] a, float[] b) {
        float dot = 0.0f;
        for (int i = 0; i < 512; i++) {
            dot += a[i] * b[i];
        }
        return dot;
    }

    private static bool IsLeafPresentInKeywords(HashSet<string> keywords, string leafTag) {
        foreach (var kw in keywords) {
            if (string.IsNullOrWhiteSpace(kw)) continue;
            var parts = kw.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var leaf = parts.Length > 0 ? parts[^1].Trim() : kw.Trim();
            if (leaf.Equals(leafTag, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }
}
