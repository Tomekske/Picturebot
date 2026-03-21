using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Database.Domain.Entities;

/// <summary>
///     Represents a picture node containing metadata and analysis results.
/// </summary>
[Table("pictures")]
public class Picture : Node
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Picture" /> class.
    /// </summary>
    public Picture()
    {
        Children = null;
    }

    /// <summary>
    ///     The analysis results for the picture, including sharpness and perceptual hash.
    /// </summary>
    [Column("metrics")]
    public Metrics? Metrics { get; set; }

    /// <summary>
    ///     The current curation state of the picture (e.g., Flagged, Rejected).
    /// </summary>
    [Column("curation_status")]
    public CurationStatus CurationStatus { get; set; } = CurationStatus.Unflagged;

    /// <summary>
    ///     Calculated physical paths for various versions of the picture (RAW, Preview, Thumbnail).
    /// </summary>
    [NotMapped]
    public SubFolder? SubFolder { get; set; }
}
