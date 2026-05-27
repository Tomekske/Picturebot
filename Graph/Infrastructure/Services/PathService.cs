using Database.Domain.Entities;
using Domain.Interfaces;
using Graph.Domain.Interfaces;
using System.IO.Abstractions;

namespace Graph.Infrastructure.Services;

public class PathService(ISettingsService settingsService, IFileSystem fileSystem) : IPathService {
    public void PopulatePaths(Picture picture) {
        if (picture.Parent is not Album album) return;
        if (string.IsNullOrEmpty(album.Uuid)) return;
        if (string.IsNullOrEmpty(settingsService.Current.LibraryPath)) return;

        var albumPath = fileSystem.Path.Combine(settingsService.Current.LibraryPath, album.Uuid);
        
        var rawExtension = picture.Extension ?? string.Empty;
        
        picture.SubFolder = new SubFolder {
            Raw = fileSystem.Path.Combine(albumPath, "RAWs", picture.Name + rawExtension),
            Preview = fileSystem.Path.Combine(albumPath, "JPGs", picture.Name + ".jpg"),
            Thumbnail = fileSystem.Path.Combine(albumPath, "Thumbnails", picture.Name + ".jpg"),
            Picked = fileSystem.Path.Combine(albumPath, "Picked", picture.Name + ".jpg")
        };
    }

    public void PopulatePaths(IEnumerable<Picture> pictures) {
        foreach (var picture in pictures) {
            PopulatePaths(picture);
        }
    }

    public string? GetAlbumPickedPath(Album album) {
        if (string.IsNullOrEmpty(album.Uuid)) return null;
        if (string.IsNullOrEmpty(settingsService.Current.LibraryPath)) return null;

        var albumPath = fileSystem.Path.Combine(settingsService.Current.LibraryPath, album.Uuid);
        return fileSystem.Path.Combine(albumPath, "Picked");
    }
}
