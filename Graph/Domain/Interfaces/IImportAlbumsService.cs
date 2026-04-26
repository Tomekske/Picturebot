using Database.Domain.Entities;
using Graph.Domain.DTOs;

namespace Graph.Domain.Interfaces;

public interface IImportAlbumsService {
    Task ImportRecursiveAsync(int? parentId, string sourcePath, string libraryPath, IProgress<ImportBatchProgress>? progress = null);
}
