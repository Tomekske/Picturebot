using ErrorOr;

namespace PictureWorker.Domain.Interfaces;

public interface IPictureAnalyzer {
    Task<ErrorOr<ulong>> CalculateHashAsync(string filePath);
    Task<ErrorOr<int>> CalculateSharpnessAsync(string filePath);
}
