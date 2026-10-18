using System.IO.Abstractions;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;
using PictureWorker.Domain.Interfaces;

namespace Graph.Infrastructure.Utilities;

/// <summary>
///     Makes sure a picture has a known orientation, reading the source file's EXIF at most once.
///     Once the EXIF code is stored in the XMP (marked via <see cref="Picture.OrientationFromExif" />), no file is touched again.
/// </summary>
public static class PictureOrientationResolver {
    /// <summary>
    ///     Resolves the orientation of a picture whose XMP metadata has already been loaded.
    /// </summary>
    /// <returns>True when the orientation was (re)calculated and saved to XMP.</returns>
    public static async Task<bool> EnsureAsync(Picture pic, IPictureAnalyzer? pictureAnalyzer, IXmpService xmpService, IFileSystem fileSystem) {
        // Already verified against EXIF on a previous load or at import
        if (pic.OrientationFromExif && pic.Orientation != Orientation.Unknown) return false;

        var rawFile = pic.SubFolder?.Raw;
        var imgPath = pic.SubFolder?.Preview ?? pic.SubFolder?.Thumbnail ?? pic.SubFolder?.Raw;
        var fileToInspect = (!string.IsNullOrEmpty(rawFile) && fileSystem.File.Exists(rawFile)) ? rawFile : imgPath;

        if (!string.IsNullOrEmpty(fileToInspect) && fileSystem.File.Exists(fileToInspect) && pictureAnalyzer != null) {
            var oriResult = await pictureAnalyzer.ExtractOrientationAsync(fileToInspect);
            if (!oriResult.IsError) {
                var exifCode = oriResult.Value is >= 1 and <= 8 ? oriResult.Value : 1;
                pic.RawExifOrientation = exifCode;
                pic.Orientation = FromExif(exifCode, pic.Width, pic.Height);
                pic.OrientationFromExif = true;
                await xmpService.SaveMetadataAsync(pic);
                return true;
            }
        }

        // EXIF unavailable: only fill in a missing orientation, without the marker so EXIF is retried next load
        if (pic.Orientation != Orientation.Unknown) return false;

        if (pic.Width > 0 && pic.Height > 0) {
            pic.Orientation = pic.Width < pic.Height ? Orientation.Portrait : Orientation.Landscape;
            await xmpService.SaveMetadataAsync(pic);
            return true;
        }

        if (!string.IsNullOrEmpty(imgPath) && fileSystem.File.Exists(imgPath) && pictureAnalyzer != null) {
            var dimResult = await pictureAnalyzer.GetDimensionsAsync(imgPath);
            if (!dimResult.IsError && dimResult.Value.Width > 0 && dimResult.Value.Height > 0) {
                pic.Width = dimResult.Value.Width;
                pic.Height = dimResult.Value.Height;
                pic.Orientation = pic.Width < pic.Height ? Orientation.Portrait : Orientation.Landscape;
                await xmpService.SaveMetadataAsync(pic);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     EXIF codes 5-8 are rotated 90/270 degrees (portrait); otherwise fall back to the pixel dimensions.
    /// </summary>
    public static Orientation FromExif(int exifCode, int width, int height) {
        return exifCode is 5 or 6 or 7 or 8 || (width > 0 && height > 0 && width < height)
            ? Orientation.Portrait
            : Orientation.Landscape;
    }
}
