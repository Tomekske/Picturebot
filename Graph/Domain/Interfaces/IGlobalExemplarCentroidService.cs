using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Graph.Domain.Interfaces;

/// <summary>
///     Service for querying global database exemplar embeddings, calculating unit-normalized leaf centroids,
///     and maintaining dynamic centroid updates upon tag addition or removal.
/// </summary>
public interface IGlobalExemplarCentroidService {
    /// <summary>
    ///     Minimum exemplar count required to activate auto-discovery for a leaf tag (default: N = 10).
    /// </summary>
    int MinimumExemplarThreshold { get; set; }

    /// <summary>
    ///     Gets unit-normalized centroid vectors for all active leaf tags where |E_T| >= N across the global database catalog.
    /// </summary>
    Task<Dictionary<string, float[]>> GetActiveLeafCentroidsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Recalculates or updates the centroid when a positive tag exemplar is added to an image.
    /// </summary>
    void OnTagAdded(int pictureId, string tag, float[] embedding);

    /// <summary>
    ///     Recalculates or updates the centroid when a tag exemplar is removed from an image.
    /// </summary>
    void OnTagRemoved(int pictureId, string tag, float[] embedding);
}
