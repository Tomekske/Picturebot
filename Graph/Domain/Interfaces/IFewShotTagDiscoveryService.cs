using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

/// <summary>
///     Result details from running automated few-shot tag discovery on an image.
/// </summary>
public record TagDiscoveryResult(
    Picture Picture,
    List<string> DiscoveredLeafTags,
    List<string> ResolvedFlatTags,
    List<string> ResolvedHierarchicalTags
);

/// <summary>
///     Service for automated few-shot tag discovery & non-destructive XMP auto-save pipeline.
/// </summary>
public interface IFewShotTagDiscoveryService {
    /// <summary>
    ///     Similarity confidence threshold for triggering a tag match (default: tau = 0.85).
    /// </summary>
    float SimilarityThreshold { get; set; }

    /// <summary>
    ///     Executes few-shot tag discovery across pictures in an album, expanding taxonomy and auto-saving XMP sidecars.
    /// </summary>
    Task<List<TagDiscoveryResult>> ScanPicturesAsync(
        List<Picture> pictures,
        Action<Picture, List<string>>? onTagsDiscoveredOnUIThread = null,
        CancellationToken cancellationToken = default);
}
