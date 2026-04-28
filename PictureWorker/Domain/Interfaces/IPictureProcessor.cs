using ErrorOr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace PictureWorker.Domain.Interfaces;

public interface IPictureProcessor {
    Task<ErrorOr<Image>> GenerateProcessedImageAsync(string filePath, int maxWidth, int maxHeight,
        IResampler? resampler = null);
}
