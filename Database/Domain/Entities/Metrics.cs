using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Domain.Entities;

/// <summary>
///     Represents analysis metrics for a specific picture.
/// </summary>
[Table("metrics")]
public class Metrics {
    /// <summary>
    ///     The unique identifier for the metrics, corresponding to the picture it belongs to.
    /// </summary>
    [Key]
    [ForeignKey(nameof(Picture))]
    public int PictureId { get; set; }

    /// <summary>
    ///     An integer score representing the focus quality of the picture.
    /// </summary>
    public int? Sharpness { get; set; }

    /// <summary>
    ///     A 64-bit perceptual hash used for identifying visually similar pictures.
    /// </summary>
    public ulong? PHash { get; set; }

    /// <summary>
    ///     A 512-dimensional float vector embedding represented as a byte array for database storage.
    /// </summary>
    public byte[]? Embedding { get; set; }

    /// <summary>
    ///     The picture these metrics belong to.
    /// </summary>
    public Picture? Picture { get; set; }

    /// <summary>
    ///     Gets the 512-dimensional float vector embedding from the raw byte array.
    /// </summary>
    public float[]? GetEmbeddingVector() {
        if (Embedding == null || Embedding.Length == 0) return null;
        var floats = new float[Embedding.Length / sizeof(float)];
        Buffer.BlockCopy(Embedding, 0, floats, 0, Embedding.Length);
        return floats;
    }

    /// <summary>
    ///     Sets the raw byte array from a 512-dimensional float vector embedding.
    /// </summary>
    public void SetEmbeddingVector(float[]? vector) {
        if (vector == null || vector.Length == 0) {
            Embedding = null;
            return;
        }
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        Embedding = bytes;
    }
}
