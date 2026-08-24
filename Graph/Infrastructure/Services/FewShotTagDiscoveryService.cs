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

    public float SimilarityThreshold { get; set; } = 0.85f;

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
            return new List<TagDiscoveryResult>();
        }

        var results = new List<TagDiscoveryResult>();

        // 2. Process each picture
        foreach (var picture in pictures) {
            cancellationToken.ThrowIfCancellationRequested();

            // Lock baseline committed tags (existing tags loaded from XMP sidecar)
            var committedTags = new HashSet<string>(picture.Keywords ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            // Ingest / load image embedding vector
            var embedding = await _embeddingService.GetOrComputeEmbeddingAsync(picture, cancellationToken);
            if (embedding == null || embedding.Length != 512) {
                continue;
            }

            var discoveredLeafs = new List<string>();
            var newFlatTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newHierarchicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 3. Match against active leaf centroids
            foreach (var (leafTag, centroid) in centroids) {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if leaf tag is already committed
                if (committedTags.Contains(leafTag) || IsLeafPresentInKeywords(committedTags, leafTag)) {
                    continue;
                }

                // Compute Cosine Similarity (both embedding and centroid are unit-normalized)
                float sim = ComputeCosineSimilarity(embedding, centroid);

                if (sim >= SimilarityThreshold) {
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
            }

            if (discoveredLeafs.Count == 0) {
                continue;
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

            // Trigger live UI thread callback if provided
            onTagsDiscoveredOnUIThread?.Invoke(picture, picture.Keywords);

            // 6. Asynchronous Non-Destructive XMP Auto-Save
            try {
                await _xmpService.SaveMetadataAsync(picture);
            } catch (Exception ex) {
                Log.Error(ex, "Failed auto-saving XMP sidecar after tag discovery for picture {Name}", picture.Name);
            }

            // Update SQLite database KeywordsJson
            if (picture.Id > 0) {
                try {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var dbPic = await dbContext.Pictures.FirstOrDefaultAsync(p => p.Id == picture.Id, cancellationToken);
                    if (dbPic != null) {
                        dbPic.KeywordsJson = picture.KeywordsJson;
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                } catch (Exception ex) {
                    Log.Warning(ex, "Failed persisting updated KeywordsJson to SQLite for picture {PictureId}", picture.Id);
                }
            }

            results.Add(new TagDiscoveryResult(
                picture,
                discoveredLeafs,
                newFlatTags.ToList(),
                newHierarchicalPaths.ToList()
            ));
        }

        return results;
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
