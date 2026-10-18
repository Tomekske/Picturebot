using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Material.Icons;

namespace Picturebot.ViewModels;

/// <summary>
///     A folder in the New dialog's location picker. Pending folders don't exist yet and are created on Create.
/// </summary>
public class LocationFolder {
    public LocationFolder(int? id, string name, LocationFolder? parent, bool isPending = false) {
        Id = id;
        Name = name;
        Parent = parent;
        IsPending = isPending;
    }

    // Set once a pending folder has been created, so a retry after a partial failure doesn't create it twice.
    public int? Id { get; set; }
    public string Name { get; }
    public LocationFolder? Parent { get; }
    public bool IsPending { get; set; }
    public List<LocationFolder> Folders { get; } = new();
    public List<string> AlbumNames { get; } = new();

    public IEnumerable<LocationFolder> Ancestors {
        get {
            for (var current = Parent; current != null; current = current.Parent) {
                yield return current;
            }
        }
    }

    /// <summary>
    ///     "Trips › Asia › Japan", without the Library root.
    /// </summary>
    public string Path => string.Join(" › ", Ancestors.Reverse().Select(a => a.Name).Append(Name));

    public string ParentPath => Parent == null ? "Library" : Parent.Path;

    public string ContentSummary {
        get {
            if (IsPending) return "new";
            var parts = new List<string>();
            if (Folders.Count > 0) parts.Add(Folders.Count == 1 ? "1 folder" : $"{Folders.Count} folders");
            if (AlbumNames.Count > 0) parts.Add(AlbumNames.Count == 1 ? "1 album" : $"{AlbumNames.Count} albums");
            return parts.Count == 0 ? "empty" : string.Join(" · ", parts);
        }
    }

    public bool ContainsName(string name) {
        return Folders.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
               || AlbumNames.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }
}

public enum LocationRowKind {
    Child,
    Recent,
    SearchResult,
    Create
}

/// <summary>
///     One row in the location list: a child folder, a recent location, a search hit, or a "create" action.
/// </summary>
public class LocationRow {
    public required LocationRowKind Kind { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Badge { get; init; }
    public required ICommand Command { get; init; }

    public MaterialIconKind Icon => Kind switch {
        LocationRowKind.Recent => MaterialIconKind.History,
        LocationRowKind.Create => MaterialIconKind.FolderPlusOutline,
        _ => MaterialIconKind.Folder
    };

    public bool IsCreate => Kind == LocationRowKind.Create;
    public bool IsFolder => Kind != LocationRowKind.Create;
    public bool ShowChevron => Kind == LocationRowKind.Child;
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
    public bool HasBadge => !string.IsNullOrEmpty(Badge);
}

/// <summary>
///     A clickable segment of the path bar.
/// </summary>
public class PathSegment {
    public required string Name { get; init; }
    public bool IsPending { get; init; }
    public bool IsFirst { get; init; }
    public bool IsLast { get; init; }
    public required ICommand Command { get; init; }
}
