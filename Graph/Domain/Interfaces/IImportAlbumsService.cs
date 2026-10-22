using Database.Domain.Entities;
using Graph.Domain.DTOs;

namespace Graph.Domain.Interfaces;

public interface IImportAlbumsService {
    /// <returns>The node created for the source folder itself.</returns>
    Task<Node> ImportRecursiveAsync(int? parentId, string sourcePath, string libraryPath, IProgress<ImportBatchProgress>? progress = null,
        string? timeZoneId = null);
}
