using System.IO.Abstractions;
using ErrorOr;
using PictureWorker.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PictureWorker.Infrastructure.Services;

public class PictureProcessorService : IPictureProcessor {
    private readonly IFileSystem _fileSystem;

    public PictureProcessorService(IFileSystem fileSystem) {
        _fileSystem = fileSystem;
    }

    public async Task<ErrorOr<Image>> GenerateThumbnailAsync(string filePath) {
        const int widthThreshold = 960;
        const int heightThreshold = 960;

        try {
            // Read from the abstracted file system, not the physical disk
            await using var stream = _fileSystem.File.OpenRead(filePath);
            var image = await Image.LoadAsync(stream);

            // Downscale the picture if its dimensions exceed the defined threshold.
            if (image.Width > widthThreshold || image.Height > heightThreshold) {
                image.Mutate(x => x.Resize(new ResizeOptions {
                    Size = new Size(widthThreshold, heightThreshold),
                    Mode = ResizeMode.Max
                }));
            }

            return image;
        } catch (Exception ex) {
            return Error.Failure(
                "PictureProcessor.GenerationFailed",
                $"Failed to process image: {ex.Message}");
        }
    }
}
