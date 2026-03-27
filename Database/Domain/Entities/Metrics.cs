using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

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
    [Column("picture_id")]
    public int PictureId { get; set; }

    /// <summary>
    ///     An integer score representing the focus quality of the picture.
    /// </summary>
    [Column("sharpness")]
    public int? Sharpness { get; set; }

    /// <summary>
    ///     A 64-bit perceptual hash used for identifying visually similar pictures.
    /// </summary>
    [Column("phash")]
    public ulong? PHash { get; set; }

    /// <summary>
    ///     The picture these metrics belong to.
    /// </summary>
    [JsonIgnore]
    public Picture? Picture { get; set; }
}
