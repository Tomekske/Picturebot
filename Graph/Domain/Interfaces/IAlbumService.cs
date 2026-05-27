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

    Task DeleteAsync(Album album);

    /// <summary>
    ///     Synchronizes the database curation status of pictures within the album based on the presence of files in the physical 'Picked' subfolder.
    /// </summary>
    /// <param name="album">The album to synchronize.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task SyncPickedStatusAsync(Album album);
}
