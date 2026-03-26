namespace Database.Domain.Entities;

/// <summary>
///     Holds the absolute file system paths for the different versions of a picture.
/// </summary>
public class SubFolder {
    /// <summary>
    ///     The absolute path to the RAW version of the picture.
    /// </summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>
    ///     The absolute path to the generated thumbnail of the picture.
    /// </summary>
    public string Thumbnail { get; set; } = string.Empty;

    /// <summary>
    ///     The absolute path to the high-quality preview (JPG) of the picture.
    /// </summary>
    public string Preview { get; set; } = string.Empty;
}
