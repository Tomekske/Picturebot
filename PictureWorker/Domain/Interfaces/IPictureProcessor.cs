using ErrorOr;
using SixLabors.ImageSharp;

namespace PictureWorker.Domain.Interfaces;

public interface IPictureProcessor {
    Task<ErrorOr<Image>> GenerateProcessedImageAsync(string filePath, int maxWidth, int maxHeight);
}
