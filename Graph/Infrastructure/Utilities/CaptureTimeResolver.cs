using System.IO.Abstractions;
using Database.Domain.Entities;
using Domain.TimeZones;
using Graph.Domain.Interfaces;
using PictureWorker.Domain.Interfaces;

namespace Graph.Infrastructure.Utilities;

/// <summary>
///     Works out when a picture was taken in the album's time zone. The camera's UTC offset is read from EXIF
///     at most once per picture and kept in the XMP; the conversion itself is redone on every load.
/// </summary>
public static class CaptureTimeResolver {
    /// <summary>
    ///     Reads the camera's UTC offset from EXIF for a picture whose XMP metadata has already been loaded.
    /// </summary>
    /// <returns>True when the offset was read and saved to XMP.</returns>
    public static async Task<bool> EnsureCameraOffsetAsync(Picture pic, IPictureAnalyzer? pictureAnalyzer,
        IXmpService xmpService, IFileSystem fileSystem) {
        if (pic.CameraUtcOffsetChecked || pictureAnalyzer == null) return false;

        // The JPG is much smaller to open than the RAW and carries the same EXIF
        var fileToInspect = new[] { pic.SubFolder?.Preview, pic.SubFolder?.Raw }
            .FirstOrDefault(path => !string.IsNullOrEmpty(path) && fileSystem.File.Exists(path));
        if (fileToInspect == null) return false;

        var offsetResult = await pictureAnalyzer.ExtractUtcOffsetAsync(fileToInspect);
        // A file that couldn't be read is retried on the next load
        if (offsetResult.IsError) return false;

        pic.CameraUtcOffset = offsetResult.Value;
        pic.CameraUtcOffsetChecked = true;
        await xmpService.SaveMetadataAsync(pic);
        return true;
    }

    /// <summary>
    ///     Sets <see cref="Picture.LocalCapturedAt" /> from the camera time. The camera clock is read with its EXIF
    ///     offset, or as running on the default zone when it has none; the result is shown in the album's zone,
    ///     or in the default zone for albums without one.
    /// </summary>
    public static void Apply(Picture pic, string? albumZoneId, string? configuredDefaultZoneId, string? sourceLabel = null) {
        var defaultZoneId = TimeZoneCatalog.DefaultZoneId(configuredDefaultZoneId);
        var targetZoneId = string.IsNullOrWhiteSpace(albumZoneId) ? defaultZoneId : albumZoneId;
        pic.LocalTimeZoneSource = string.IsNullOrWhiteSpace(albumZoneId) ? "default" : sourceLabel;

        var local = CaptureTime.ToLocal(pic.CapturedAt, pic.CameraUtcOffset, defaultZoneId, targetZoneId);
        pic.LocalCapturedAt = local.Local;
        pic.LocalTimeZoneId = local.ZoneId;
        pic.LocalUtcOffset = local.UtcOffset;
    }

    public static void Apply(IEnumerable<Picture> pictures, string? albumZoneId, string? configuredDefaultZoneId,
        string? sourceLabel = null) {
        foreach (var pic in pictures) {
            Apply(pic, albumZoneId, configuredDefaultZoneId, sourceLabel);
        }
    }

    /// <summary>
    ///     "set on album" when the album has its own zone, "from Miami" when it comes from a folder above.
    /// </summary>
    public static string? SourceLabel(TimeZoneSource? source, int albumId) {
        if (source == null) return null;
        return source.NodeId == albumId ? "set on album" : $"from {source.NodeName}";
    }
}
