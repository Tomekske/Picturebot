using System.Collections.Generic;
using Database.Domain.Entities;

namespace Picturebot.ViewModels;

public class LocationItem {
    public string Name { get; set; } = string.Empty;
    public int? Id { get; set; }
    public bool IsAction { get; set; }
    public bool IsSeparator { get; set; }

    public static string GetFolderPath(Folder folder, Dictionary<int, Folder> folderMap) {
        var pathParts = new List<string>();
        var current = folder;
        while (current != null) {
            pathParts.Add(current.Name);
            if (current.ParentId.HasValue && folderMap.TryGetValue(current.ParentId.Value, out var parent)) {
                current = parent;
            } else {
                break;
            }
        }
        pathParts.Reverse();
        return "Library / " + string.Join(" / ", pathParts);
    }
}
