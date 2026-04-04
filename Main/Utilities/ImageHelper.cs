using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Main.Utilities;

public static class ImageHelper {
    /// <summary>
    /// Loads an image, applies EXIF orientation, and decodes it to a specific height while maintaining aspect ratio.
    /// This ensures portrait images are vertical and not distorted.
    /// </summary>
    public static async Task<Bitmap> LoadAndOrientAsync(string path, int targetHeight) {
        return await Task.Run(() => {
            using var image = Image.Load(path);
            
            // 1. Correct the orientation based on EXIF
            image.Mutate(x => x.AutoOrient());

            // 2. Resize if necessary (based on height for justified layout)
            if (image.Height > targetHeight) {
                image.Mutate(x => x.Resize(0, targetHeight));
            }

            // 3. Convert to Avalonia Bitmap
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder());
            ms.Position = 0;
            return new Bitmap(ms);
        });
    }
}
