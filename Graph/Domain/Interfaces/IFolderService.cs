using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

/// <summary>
///     Specialized service for folder-specific operations.
/// </summary>
public interface IFolderService {
    /// <summary>
    ///     Creates a new folder under a specific parent.
    /// </summary>
    /// <param name="parentId">The ID of the parent folder, or null for root.</param>
    /// <param name="folderName">The name of the new folder.</param>
    /// <returns>A Task that returns the created Folder entity.</returns>
    Task<Folder> CreateAsync(int? parentId, string folderName);

    Task<List<Folder>> FindAllAsync();
}
