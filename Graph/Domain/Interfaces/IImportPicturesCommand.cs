using Database.Domain.Entities;
using Graph.Domain.DTOs;

namespace Graph.Domain.Interfaces;

public interface IImportPicturesCommand {
    Task<Album> ExecuteAsync(int? parentId, string albumName, string libraryPath, string sourcePath, IProgress<ImportProgress>? progress = null);
}
