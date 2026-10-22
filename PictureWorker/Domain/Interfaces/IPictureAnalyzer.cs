using ErrorOr;

namespace PictureWorker.Domain.Interfaces;

public interface IPictureAnalyzer {
    Task<ErrorOr<ulong>> CalculateHashAsync(string filePath);
    Task<ErrorOr<int>> CalculateSharpnessAsync(string filePath);
    Task<ErrorOr<DateTime>> ExtractTimestamp(string filePath);
    Task<ErrorOr<(int Width, int Height)>> GetDimensionsAsync(string filePath);
    Task<ErrorOr<int>> ExtractOrientationAsync(string filePath);

    /// <summary>
    ///     The camera clock's UTC offset from EXIF OffsetTimeOriginal (e.g. +01:00); null when the camera didn't
    ///     write one, an error when the file couldn't be read.
    /// </summary>
    Task<ErrorOr<TimeSpan?>> ExtractUtcOffsetAsync(string filePath);
}
