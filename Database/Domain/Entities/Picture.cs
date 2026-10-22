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
    ///     The date and time the picture was captured, as the camera's clock showed it.
    /// </summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>
    ///     The camera clock's UTC offset from EXIF (OffsetTimeOriginal), loaded from and saved to XMP.
    ///     Null when the camera didn't write one.
    /// </summary>
    [NotMapped]
    public TimeSpan? CameraUtcOffset { get; set; }

    /// <summary>
    ///     True once the source file's EXIF was checked for <see cref="CameraUtcOffset" />, found or not.
    ///     Persisted as an XMP marker so the file is read only once per picture.
    /// </summary>
    [NotMapped]
    public bool CameraUtcOffsetChecked { get; set; }

    private DateTime? _localCapturedAt;

    /// <summary>
    ///     The capture time where the picture was taken (the album's time zone). Calculated when pictures are
    ///     loaded; falls back to <see cref="CapturedAt" /> until then.
    /// </summary>
    [NotMapped]
    public DateTime LocalCapturedAt {
        get => _localCapturedAt ?? CapturedAt;
        set => _localCapturedAt = value;
    }

    /// <summary>
    ///     The IANA zone <see cref="LocalCapturedAt" /> is in; null when it shows the camera time.
    /// </summary>
    [NotMapped]
    public string? LocalTimeZoneId { get; set; }

    /// <summary>
    ///     The UTC offset of <see cref="LocalCapturedAt" />, summer time included.
    /// </summary>
    [NotMapped]
    public TimeSpan? LocalUtcOffset { get; set; }

    /// <summary>
    ///     Where <see cref="LocalTimeZoneId" /> comes from, e.g. "set on album", "from Miami" or "default".
    /// </summary>
    [NotMapped]
    public string? LocalTimeZoneSource { get; set; }

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
    ///     The orientation of the picture (Landscape or Portrait), loaded from and saved to XMP.
    /// </summary>
    [NotMapped]
    public Orientation Orientation { get; set; } = Orientation.Unknown;

    /// <summary>
    ///     The raw EXIF orientation code (1, 3, 6, 8, etc.) from the camera, loaded from and saved to XMP.
    /// </summary>
    [NotMapped]
    public int RawExifOrientation { get; set; } = 0;

    /// <summary>
    ///     True when <see cref="RawExifOrientation" /> was read from the source file's EXIF (not guessed from dimensions).
    ///     Persisted as an XMP marker so orientation is only detected once per picture.
    /// </summary>
    [NotMapped]
    public bool OrientationFromExif { get; set; }

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

    /// <summary>
    ///     The keywords (tags) serialized as JSON for SQLite database indexing and querying.
    /// </summary>
    public string? KeywordsJson { get; set; }

    /// <summary>
    ///     The keywords (tags) associated with the picture, loaded from and saved to XMP.
    /// </summary>
    [NotMapped]
    public List<string> Keywords { get; set; } = new();
}
