using System.Threading.Tasks;
using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

public interface IXmpService {
    /// <summary>
    ///     Loads metadata (Rating, ColorLabel, CurationStatus, CapturedAt) from the picture's XMP sidecar file.
    ///     If the file does not exist, it sets default values.
    /// </summary>
    Task LoadMetadataAsync(Picture picture);

    /// <summary>
    ///     Saves metadata (Rating, ColorLabel, CurationStatus, CapturedAt) from the picture to the XMP sidecar file.
    ///     If the file already exists, it loads it and updates the attributes to preserve other metadata tags.
    /// </summary>
    Task SaveMetadataAsync(Picture picture);

    /// <summary>
    ///     Generates missing XMP sidecar files for an entire album using the legacy database columns.
    /// </summary>
    Task CreateXmpForAlbumAsync(int albumId);
}
