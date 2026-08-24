using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

public interface IPathService {
    void PopulatePaths(Picture picture);
    void PopulatePaths(IEnumerable<Picture> pictures);
    string? GetAlbumPickedPath(Album album);
    string? GetAlbumHighlightsPath(Album album);
    string? GetAlbumDeletedPath(Album album);
}
