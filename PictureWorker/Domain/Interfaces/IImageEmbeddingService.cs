using System.Threading;
using System.Threading.Tasks;
using Database.Domain.Entities;

namespace PictureWorker.Domain.Interfaces;

/// <summary>
///     Service for generating and managing 512-dimensional unit-normalized image embeddings.
/// </summary>
public interface IImageEmbeddingService {
    /// <summary>
    ///     Gets cached 512-d float embedding vector for a picture from SQLite or computes it if missing.
    /// </summary>
    Task<float[]> GetOrComputeEmbeddingAsync(Picture picture, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Computes a 512-d unit-normalized float embedding vector for an image file.
    /// </summary>
    Task<float[]> ComputeEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Normalizes a 512-d vector to unit L2 norm (||v||_2 = 1).
    /// </summary>
    float[] NormalizeVector(float[] vector);
}
