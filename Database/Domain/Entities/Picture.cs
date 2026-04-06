using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Database.Domain.Entities;

/// <summary>
///     Represents a picture node containing metadata and analysis results.
/// </summary>
[Table("pictures")]
public class Picture : Node {
    /// <summary>
    ///     Initializes a new instance of the <see cref="Picture" /> class.
    /// </summary>
    public Picture() {
        Children = null;
    }

    /// <summary>
    ///     The date and time the picture was captured.
    /// </summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>
    ///     The perceptual hash of the picture.
    /// </summary>
    public ulong Hash { get; set; }

    /// <summary>
    ///     The sharpness score of the picture.
    /// </summary>
    public int Sharpness { get; set; }

    /// <summary>
    ///     The analysis results for the picture, including sharpness and perceptual hash.
    /// </summary>
    public Metrics? Metrics { get; set; }

    /// <summary>
    ///     The current curation state of the picture (e.g., Flagged, Rejected).
    /// </summary>
    public CurationStatus CurationStatus { get; set; } = CurationStatus.Unflagged;

    /// <summary>
    ///     Calculated physical paths for various versions of the picture (RAW, Preview, Thumbnail).
    /// </summary>
    [NotMapped]
    public SubFolder? SubFolder { get; set; }
}
