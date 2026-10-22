using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Picturebot.Services;

namespace Picturebot.ViewModels;

/// <summary>
///     The location picker shared by the New and Edit dialogs: a path bar, one level at a time, search across all
///     folder paths, recent locations, and typing a new folder (or a path) that is created when the dialog confirms.
/// </summary>
public partial class LocationPickerViewModel : ObservableObject {
    private static readonly char[] PathSeparators = ['›', '/', '\\', '>'];

    private readonly Dictionary<int, LocationFolder> _byId = new();
    private readonly IRecentLocationsService? _recentLocations;

    [ObservableProperty]
    private LocationFolder _current;

    [ObservableProperty]
    private string _listHeader = string.Empty;

    [ObservableProperty]
    private string _query = string.Empty;

    /// <param name="excludeFolderId">A folder that can't be picked, together with everything inside it (the node being moved).</param>
    public LocationPickerViewModel(
        IEnumerable<Node> roots,
        IRecentLocationsService? recentLocations,
        int? startFolderId,
        int? excludeFolderId = null) {
        _recentLocations = recentLocations;
        Root = new LocationFolder(null, "Library", null);

        foreach (var node in roots) {
            AddNode(node, Root, excludeFolderId);
        }

        _current = startFolderId.HasValue && _byId.TryGetValue(startFolderId.Value, out var start) ? start : Root;
        Refresh();
    }

    public LocationFolder Root { get; }
    public ObservableCollection<PathSegment> PathSegments { get; } = new();
    public ObservableCollection<LocationRow> Rows { get; } = new();
    public bool HasRows => Rows.Count > 0;

    private void AddNode(Node node, LocationFolder parent, int? excludeFolderId) {
        switch (node) {
            case Folder folder: {
                if (folder.Id == excludeFolderId) return;
                var location = new LocationFolder(folder.Id, folder.Name, parent == Root ? null : parent) {
                    TimeZoneId = folder.TimeZoneId
                };
                parent.Folders.Add(location);
                _byId[folder.Id] = location;
                foreach (var child in node.Children ?? []) {
                    AddNode(child, location, excludeFolderId);
                }
                break;
            }
            case Album album:
                parent.AlbumNames.Add(album.Name);
                break;
        }
    }

    private LocationFolder? ParentOf(LocationFolder folder) {
        return folder == Root ? null : folder.Parent ?? Root;
    }

    partial void OnCurrentChanged(LocationFolder value) => Refresh();

    partial void OnQueryChanged(string value) => RebuildRows();

    [RelayCommand]
    private void NavigateTo(LocationFolder folder) {
        Query = string.Empty;
        Current = folder;
    }

    /// <summary>
    ///     Goes one level up; used by the path bar and by Backspace in an empty search box.
    /// </summary>
    public void GoUp() {
        var parent = ParentOf(Current);
        if (parent != null) {
            NavigateTo(parent);
        }
    }

    public void ActivateFirstRow() {
        var row = Rows.FirstOrDefault();
        if (row?.Command.CanExecute(null) == true) {
            row.Command.Execute(null);
        }
    }

    /// <summary>
    ///     Walks the typed path from the current folder: existing folders are entered, missing ones become pending.
    /// </summary>
    [RelayCommand]
    private void CreateHere(string path) {
        var location = Current;
        foreach (var segment in SplitPath(path)) {
            var existing = location.IsPending
                ? null
                : location.Folders.FirstOrDefault(f => string.Equals(f.Name, segment, StringComparison.OrdinalIgnoreCase));
            location = existing ?? new LocationFolder(null, segment, location == Root ? null : location, isPending: true);
        }

        NavigateTo(location);
    }

    private static List<string> SplitPath(string path) {
        return path.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    ///     Pending folders from the outermost to the current one.
    /// </summary>
    public List<LocationFolder> PendingChain() {
        if (Current == Root) return [];
        return Current.Ancestors.Reverse().Append(Current).Where(f => f.IsPending).ToList();
    }

    /// <summary>
    ///     The id of the nearest folder from the current one upwards that exists; null for the library root.
    /// </summary>
    public int? NearestExistingId() {
        return Current == Root ? null : Current.Ancestors.Prepend(Current).FirstOrDefault(f => f.Id.HasValue)?.Id;
    }

    /// <summary>
    ///     Creates the pending folders of the chosen path, outermost first, and returns the id of the chosen
    ///     location. Every folder made is added to <paramref name="created" />, also when a later one fails.
    /// </summary>
    public async Task<int?> EnsurePathAsync(IFolderService folderService, List<Folder> created) {
        foreach (var pending in PendingChain()) {
            var folder = await folderService.CreateAsync(pending.Parent?.Id, pending.Name);
            pending.Id = folder.Id;
            pending.IsPending = false;
            (pending.Parent ?? Root).Folders.Add(pending);
            _byId[folder.Id] = pending;
            created.Add(folder);
        }
        return Current.Id;
    }

    public void Refresh() {
        RebuildPathSegments();
        RebuildRows();
    }

    private void RebuildPathSegments() {
        PathSegments.Clear();
        var chain = Current == Root
            ? new List<LocationFolder> { Root }
            : Current.Ancestors.Reverse().Prepend(Root).Append(Current).ToList();

        for (var i = 0; i < chain.Count; i++) {
            var folder = chain[i];
            PathSegments.Add(new PathSegment {
                Name = folder.Name,
                IsPending = folder.IsPending,
                IsFirst = i == 0,
                IsLast = i == chain.Count - 1,
                Command = new RelayCommand(() => NavigateTo(folder))
            });
        }
    }

    private void RebuildRows() {
        Rows.Clear();
        var query = Query.Trim();

        if (query.Length == 0) {
            ListHeader = Current == Root ? "Library" : $"In {Current.Name}";

            foreach (var id in _recentLocations?.GetRecent() ?? []) {
                if (!_byId.TryGetValue(id, out var recent) || recent == Current) continue;
                Rows.Add(new LocationRow {
                    Kind = LocationRowKind.Recent,
                    Title = recent.Name,
                    Subtitle = recent.ParentPath,
                    Badge = "Recent",
                    Command = new RelayCommand(() => NavigateTo(recent))
                });
            }

            foreach (var child in Current.Folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)) {
                Rows.Add(new LocationRow {
                    Kind = LocationRowKind.Child,
                    Title = child.Name,
                    Subtitle = child.ContentSummary,
                    Command = new RelayCommand(() => NavigateTo(child))
                });
            }
        } else {
            ListHeader = "Matching folders";
            var tokens = query.ToLowerInvariant()
                .Split(PathSeparators.Append(' ').ToArray(), StringSplitOptions.RemoveEmptyEntries);
            var last = tokens.LastOrDefault() ?? string.Empty;

            var matches = _byId.Values
                .Select(f => (Folder: f, Path: f.Path.ToLowerInvariant()))
                .Where(m => tokens.All(t => m.Path.Contains(t)))
                .OrderBy(m => m.Folder.Name.StartsWith(last, StringComparison.OrdinalIgnoreCase) ? 0
                    : m.Folder.Name.Contains(last, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(m => m.Folder.Ancestors.Count())
                .ThenBy(m => m.Path)
                .Take(50);

            foreach (var match in matches) {
                var folder = match.Folder;
                Rows.Add(new LocationRow {
                    Kind = LocationRowKind.SearchResult,
                    Title = folder.Name,
                    Subtitle = folder.ParentPath,
                    Command = new RelayCommand(() => NavigateTo(folder))
                });
            }

            var segments = SplitPath(query);
            var alreadyChild = segments.Count == 1 && !Current.IsPending &&
                               Current.Folders.Any(f => string.Equals(f.Name, segments[0], StringComparison.OrdinalIgnoreCase));
            if (segments.Count > 0 && !alreadyChild) {
                var where = Current == Root ? "the library root" : Current.Name;
                Rows.Add(new LocationRow {
                    Kind = LocationRowKind.Create,
                    Title = segments.Count == 1
                        ? $"Create folder “{segments[0]}” in {where}"
                        : $"Create path “{string.Join(" › ", segments)}” in {where}",
                    Subtitle = "Created when you confirm",
                    Command = new RelayCommand(() => CreateHere(query))
                });
            }
        }

        OnPropertyChanged(nameof(HasRows));
    }
}
