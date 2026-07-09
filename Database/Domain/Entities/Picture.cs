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
    ///     The width of the picture in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     The height of the picture in pixels.
    /// </summary>
    public int Height { get; set; }

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
    [NotMapped]
    public CurationStatus CurationStatus { get; set; } = CurationStatus.Unflagged;

    /// <summary>
    ///     The color label assigned to the picture.
    /// </summary>
    [NotMapped]
    public ColorLabel ColorLabel { get; set; } = ColorLabel.None;

    /// <summary>
    ///     The star rating of the picture (0-5).
    /// </summary>
    [NotMapped]
    public int Rating { get; set; }

    /// <summary>
    ///     The extension of the raw picture file (e.g., .ARW, .CR2).
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    ///     The background processing state of the picture.
    /// </summary>
    public ProcessingState ProcessingState { get; set; } = ProcessingState.Pending;

    /// <summary>
    ///     The number of times processing has been attempted.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     The error message from the last failed processing attempt.
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    ///     Calculated physical paths for various versions of the picture (RAW, Preview, Thumbnail).
    /// </summary>
    [NotMapped]
    public SubFolder? SubFolder { get; set; }
}
