using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

/// <summary>
///     Specialized service for album-specific operations.
/// </summary>
public interface IAlbumService {
    /// <summary>
    ///     Creates a new album under a specific parent, generating a UUID for the physical folder.
    /// </summary>
    /// <param name="parentId">The ID of the parent folder, or null for root.</param>
    /// <param name="albumName">The display name of the new album.</param>
    /// <param name="path">The base path where the album directory should be created.</param>
    /// <returns>A Task that returns the created Album entity.</returns>
    Task<Album> CreateAsync(int? parentId, string albumName, string path);
}
