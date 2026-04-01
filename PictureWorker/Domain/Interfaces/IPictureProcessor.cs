using ErrorOr;
using SixLabors.ImageSharp;

namespace PictureWorker.Domain.Interfaces;

public interface IPictureProcessor {
    Task<ErrorOr<Image>> GenerateThumbnailAsync(string filePath);
}
