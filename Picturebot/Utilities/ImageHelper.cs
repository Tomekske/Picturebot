using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Picturebot.Utilities;

public static class ImageHelper {
    /// <summary>
    ///     Applies the corresponding transformation based on the standard EXIF orientation code (1-8).
    /// </summary>
    public static void ApplyOrientation(IImageProcessingContext context, int rawExifOrientation) {
        switch (rawExifOrientation) {
            case 2:
                context.Flip(FlipMode.Horizontal);
                break;
            case 3:
                context.Rotate(RotateMode.Rotate180);
                break;
            case 4:
                context.Flip(FlipMode.Vertical);
                break;
            case 5:
                context.Rotate(RotateMode.Rotate90).Flip(FlipMode.Horizontal);
                break;
            case 6:
                context.Rotate(RotateMode.Rotate90);
                break;
            case 7:
                context.Rotate(RotateMode.Rotate270).Flip(FlipMode.Horizontal);
                break;
            case 8:
                context.Rotate(RotateMode.Rotate270);
                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Loads an image, applies EXIF orientation (using auto-orient or known rawExifOrientation if not yet oriented),
    ///     and decodes it to a specific height while maintaining aspect ratio.
    /// </summary>
    public static async Task<Bitmap> LoadAndOrientAsync(string path, int targetHeight, int rawExifOrientation = 0) {
        return await Task.Run(() => {
            using var image = Image.Load(path);

            var hasExifOrientation = image.Metadata.ExifProfile != null &&
                image.Metadata.ExifProfile.TryGetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, out var orientationValue) &&
                orientationValue.Value > 1;

            if (hasExifOrientation) {
                image.Mutate(x => x.AutoOrient());
            } else if (rawExifOrientation > 1) {
                var isPortraitExif = rawExifOrientation is 5 or 6 or 7 or 8;
                var isAlreadyPortrait = image.Height > image.Width;
                if ((isPortraitExif && !isAlreadyPortrait) || (!isPortraitExif && isAlreadyPortrait)) {
                    image.Mutate(x => ApplyOrientation(x, rawExifOrientation));
                }
            }

            // Resize if necessary (based on height for justified layout)
            if (image.Height > targetHeight) {
                image.Mutate(x => x.Resize(0, targetHeight));
            }

            // Convert to Avalonia Bitmap
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder());
            ms.Position = 0;
            return new Bitmap(ms);
        });
    }

    /// <summary>
    ///     Loads an image directly into an Avalonia Bitmap via a FileStream on a background thread.
    ///     Use this for pre-oriented and pre-downsampled images (like thumbnails) to avoid ImageSharp overhead.
    /// </summary>
    public static async Task<Bitmap> LoadDirectAsync(string path) {
        return await Task.Run(() => {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        });
    }
}
