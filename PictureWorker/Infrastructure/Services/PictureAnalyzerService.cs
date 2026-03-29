using CoenM.ImageHash.HashAlgorithms;
using ErrorOr;
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
        } catch (Exception e) {
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
        } catch (Exception e) {
            return Error.Failure(
                "Picture.ProcessingFailed",
                "An unexpected error occurred while calculating sharpness.");
        }
    }
}
