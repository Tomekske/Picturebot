using System.Globalization;
using CoenM.ImageHash.HashAlgorithms;
using ErrorOr;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using OpenCvSharp;
using PictureWorker.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Size = OpenCvSharp.Size;

namespace PictureWorker.Infrastructure.Services;

public class PictureAnalyzerService : IPictureAnalyzer {
    public async Task<ErrorOr<ulong>> CalculateHashAsync(string filePath) {
        // Check existence before allocating resources or starting I/O
        if (!File.Exists(filePath)) {
            return Error.NotFound(
                "Picture.NotFound",
                $"The file was not found at path: {filePath}");
        }

        try {
            var hashAlgorithm = new PerceptualHash();

            using var picture = await Image.LoadAsync<Rgba32>(filePath);

            return hashAlgorithm.Hash(picture);
        } catch (UnknownImageFormatException) {
            return Error.Validation(
                "Picture.InvalidFormat",
                "The file is not a supported image format.");
        } catch (Exception) {
            return Error.Failure(
                "Picture.ProcessingFailed",
                "An unexpected error occurred while calculating the hash.");
        }
    }

    public async Task<ErrorOr<int>> CalculateSharpnessAsync(string filePath) {
        // Check existence before allocating resources or starting I/O
        if (!File.Exists(filePath)) {
            return Error.NotFound(
                "Picture.NotFound",
                $"The file was not found at path: {filePath}");
        }

        try {
            // 2. Offload the heavy OpenCV CPU work to a background thread
            return await Task.Run<ErrorOr<int>>(() => {
                // Load picture as Grayscale immediately
                using var src = Cv2.ImRead(filePath, ImreadModes.Grayscale);

                // Instead of throwing an exception, return a structured validation error
                if (src.Empty()) {
                    return Error.Validation(
                        "Picture.InvalidFormat",
                        "OpenCV failed to load the image. It may be corrupted or an unsupported format.");
                }

                // Resize if needed
                if (src.Width > 600) {
                    var scale = 600.0 / src.Width;
                    Cv2.Resize(src, src, new Size(0, 0), scale, scale);
                }

                // Compute Sobel gradients
                using var dx = new Mat();
                using var dy = new Mat();

                Cv2.Sobel(src, dx, MatType.CV_16S, 1, 0);
                Cv2.Sobel(src, dy, MatType.CV_16S, 0, 1);

                // Calculate Magnitude
                using var absDx = new Mat();
                using var absDy = new Mat();
                Cv2.ConvertScaleAbs(dx, absDx);
                Cv2.ConvertScaleAbs(dy, absDy);

                using var edges = new Mat();
                Cv2.AddWeighted(absDx, 0.5, absDy, 0.5, 0, edges);

                // Get the average intensity (The Sharpness Score)
                var mean = Cv2.Mean(edges);

                // Implicitly converts the int to ErrorOr<int>
                return (int)mean.Val0;
            });
        } catch (Exception) {
            return Error.Failure(
                "Picture.ProcessingFailed",
                "An unexpected error occurred while calculating sharpness.");
        }
    }

    public async Task<ErrorOr<DateTime>> ExtractTimestamp(string filePath) {
        // Check existence before starting I/O
        if (!File.Exists(filePath)) {
            return Error.NotFound(
                "Picture.NotFound",
                $"The file was not found at path: {filePath}");
        }

        try {
            // Offload metadata reading to a background thread as it involves synchronous I/O
            return await Task.Run<ErrorOr<DateTime>>(() => {
                var directories = ImageMetadataReader.ReadMetadata(filePath);

                // 1. Prioritize ExifSubIfdDirectory for Date/Time Original
                var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                var dateTimeOriginal = subIfdDirectory?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);

                if (TryParseMetadataDate(dateTimeOriginal, out var result)) {
                    return result;
                }

                // 2. Fallback to ExifIfd0Directory
                var ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                var dateTime = ifd0Directory?.GetDescription(ExifDirectoryBase.TagDateTime);

                if (TryParseMetadataDate(dateTime, out var result2)) {
                    return result2;
                }

                // If no metadata tags are found, return a specific error
                return Error.NotFound(
                    "Picture.MetadataNotFound",
                    "Could not find a valid creation timestamp in the image metadata.");
            });
        } catch (Exception) {
            // Log the error here if you have a logger injected
            return Error.Failure(
                "Picture.MetadataExtractionFailed",
                "An unexpected error occurred while extracting metadata.");
        }
    }

    public async Task<ErrorOr<(int Width, int Height)>> GetDimensionsAsync(string filePath) {
        if (!File.Exists(filePath)) {
            return Error.NotFound(
                "Picture.NotFound",
                $"The file was not found at path: {filePath}");
        }

        try {
            // Use ImageSharp's IdentifyAsync as it's very fast and efficient for just dimensions
            var info = await Image.IdentifyAsync(filePath);
            if (info == null) {
                return Error.Validation("Picture.InvalidFormat", "Failed to identify image dimensions.");
            }

            var width = info.Width;
            var height = info.Height;

            // We need the orientation to know if we should swap width and height
            // We use MetadataExtractor here as it's already working in this service
            return await Task.Run<ErrorOr<(int Width, int Height)>>(() => {
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                var ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

                if (ifd0Directory != null && ifd0Directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation)) {
                    // Orientation values 5-8 indicate the image is rotated 90 or 270 degrees
                    if (orientation > 4) {
                        return (height, width);
                    }
                }

                return (width, height);
            });
        } catch (Exception) {
            return Error.Failure(
                "Picture.ProcessingFailed",
                "An unexpected error occurred while extracting dimensions.");
        }
    }

    // Helper method to keep the logic clean
    private bool TryParseMetadataDate(string? dateString, out DateTime result) {
        return DateTime.TryParseExact(
            dateString,
            "yyyy:MM:dd HH:mm:ss",
            null,
            DateTimeStyles.None,
            out result);
    }
}
