using System.IO.Abstractions;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;

namespace Graph.Infrastructure.Services;

public class PickedService(IFileSystem fileSystem, IPathService pathService) : IPickedService {
    public async Task SyncToPickedAsync(Picture picture) {
        if (picture.SubFolder == null) {
            pathService.PopulatePaths(picture);
        }

        if (picture.SubFolder == null) return;

        var previewPath = picture.SubFolder.Preview;
        var pickedPath = picture.SubFolder.Picked;

        if (picture.CurationStatus == CurationStatus.Flagged) {
            if (fileSystem.File.Exists(previewPath)) {
                // Ensure directory exists (though it should be created at album creation)
                var directory = fileSystem.Path.GetDirectoryName(pickedPath);
                if (directory != null && !fileSystem.Directory.Exists(directory)) {
                    fileSystem.Directory.CreateDirectory(directory);
                }

                // Copy preview to picked
                // Use FileStream for async-ish copy if preferred, or just File.Copy
                await Task.Run(() => fileSystem.File.Copy(previewPath, pickedPath, true));
            }
        } else {
            // If not flagged, remove from picked if it exists
            if (fileSystem.File.Exists(pickedPath)) {
                await Task.Run(() => fileSystem.File.Delete(pickedPath));
            }
        }
    }
}
