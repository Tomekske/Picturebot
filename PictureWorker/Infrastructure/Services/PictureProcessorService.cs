using System.IO.Abstractions;
using ErrorOr;
using PictureWorker.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace PictureWorker.Infrastructure.Services;

public class PictureProcessorService : IPictureProcessor {
    private readonly IFileSystem _fileSystem;

    public PictureProcessorService(IFileSystem fileSystem) {
        _fileSystem = fileSystem;
    }

    public async Task<ErrorOr<Image>> GenerateProcessedImageAsync(string filePath, int maxWidth, int maxHeight, IResampler? resampler = null) {
        try {
            // Read from the abstracted file system, not the physical disk
            await using var stream = _fileSystem.File.OpenRead(filePath);
            var image = await Image.LoadAsync(stream);
            
            // Auto-orient based on EXIF metadata
            image.Mutate(x => x.AutoOrient());

            // Downscale the picture if its dimensions exceed the defined threshold.
            if (image.Width > maxWidth || image.Height > maxHeight) {
                image.Mutate(x => x.Resize(new ResizeOptions {
                    Size = new Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Max,
                    Sampler = resampler ?? KnownResamplers.Bicubic
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
