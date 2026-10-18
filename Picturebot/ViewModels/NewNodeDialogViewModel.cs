using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Domain.Interfaces;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.Services;
using Picturebot.Views;
using Serilog;

namespace Picturebot.ViewModels;

/// <summary>
///     Outcome of the New dialog. CreatedFolders holds every folder created on the way, also when the final step failed.
/// </summary>
public record NewNodeResult(Node? Node, IReadOnlyList<Folder> CreatedFolders, string? Error);

/// <summary>
///     The combined New Album / New Folder dialog: one location picker with a path bar, one level at a time,
///     search across all folder paths, recent locations, and creating missing folders in one go.
/// </summary>
public partial class NewNodeDialogViewModel : ViewModelBase {
    private static readonly char[] PathSeparators = ['›', '/', '\\', '>'];
    private static readonly HashSet<string> ImageExtensions =
        new(SupportedImageExtensions.AllExtensions, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<int, LocationFolder> _byId = new();
    private readonly IFolderService _folderService;
    private readonly Action<NewNodeResult> _onResult;
    private readonly IRecentLocationsService _recentLocations;
    private readonly LocationFolder _root = new(null, "Library", null);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _sourceCountCts;
    private int? _sourceFileCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private LocationFolder _current;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isAlbumMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _listHeader = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _summaryDetail = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string? _validationMessage;

    public NewNodeDialogViewModel(
        IFolderService folderService,
        ISettingsService settingsService,
        IServiceScopeFactory scopeFactory,
        IRecentLocationsService recentLocations,
        IEnumerable<Node> roots,
        int? startFolderId,
        bool startAsAlbum,
        Action<NewNodeResult> onResult) {
        _folderService = folderService;
        _settingsService = settingsService;
        _scopeFactory = scopeFactory;
        _recentLocations = recentLocations;
        _onResult = onResult;

        foreach (var node in roots) {
            AddNode(node, _root);
        }

        _isAlbumMode = startAsAlbum;
        _current = startFolderId.HasValue && _byId.TryGetValue(startFolderId.Value, out var start) ? start : _root;
        Refresh();
    }

    public ObservableCollection<PathSegment> PathSegments { get; } = new();
    public ObservableCollection<LocationRow> Rows { get; } = new();

    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);
    public bool HasRows => Rows.Count > 0;
    public string Title => IsAlbumMode ? "New Album" : "New Folder";
    public string CreateButtonText => IsAlbumMode ? "Create Album" : "Create Folder";
    public string NameWatermark => IsAlbumMode ? "e.g. Kyoto Day 2" : "e.g. Kyoto";

    private void AddNode(Node node, LocationFolder parent) {
        switch (node) {
            case Folder folder: {
                var location = new LocationFolder(folder.Id, folder.Name, parent == _root ? null : parent);
                parent.Folders.Add(location);
                _byId[folder.Id] = location;
                foreach (var child in node.Children ?? []) {
                    AddNode(child, location);
                }
                break;
            }
            case Album album:
                parent.AlbumNames.Add(album.Name);
                break;
        }
    }

    private LocationFolder? ParentOf(LocationFolder folder) {
        return folder == _root ? null : folder.Parent ?? _root;
    }

    partial void OnCurrentChanged(LocationFolder value) => Refresh();
    partial void OnQueryChanged(string value) => RebuildRows();
    partial void OnNameChanged(string value) => UpdateSummary();

    partial void OnIsAlbumModeChanged(bool value) {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CreateButtonText));
        OnPropertyChanged(nameof(NameWatermark));
        UpdateSummary();
    }

    partial void OnSourcePathChanged(string value) {
        _sourceFileCount = null;
        UpdateSummary();
        _ = CountSourceFilesAsync(value);
    }

    [RelayCommand]
    private void SetMode(string mode) {
        IsAlbumMode = mode == "Album";
    }

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
            location = existing ?? new LocationFolder(null, segment, location == _root ? null : location, isPending: true);
        }

        NavigateTo(location);
    }

    private static List<string> SplitPath(string path) {
        return path.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private void Refresh() {
        RebuildPathSegments();
        RebuildRows();
        UpdateSummary();
    }

    private void RebuildPathSegments() {
        PathSegments.Clear();
        var chain = Current == _root
            ? new List<LocationFolder> { _root }
            : Current.Ancestors.Reverse().Prepend(_root).Append(Current).ToList();

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
            ListHeader = Current == _root ? "Library" : $"In {Current.Name}";

            foreach (var id in _recentLocations.GetRecent()) {
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
                var where = Current == _root ? "the library root" : Current.Name;
                Rows.Add(new LocationRow {
                    Kind = LocationRowKind.Create,
                    Title = segments.Count == 1
                        ? $"Create folder “{segments[0]}” in {where}"
                        : $"Create path “{string.Join(" › ", segments)}” in {where}",
                    Subtitle = "Created when you click Create",
                    Command = new RelayCommand(() => CreateHere(query))
                });
            }
        }

        OnPropertyChanged(nameof(HasRows));
    }

    private void UpdateSummary() {
        var parts = Current == _root
            ? new List<string> { "Library" }
            : Current.Ancestors.Reverse().Append(Current)
                .Select(f => f.IsPending ? $"{f.Name} (new)" : f.Name)
                .Prepend("Library").ToList();
        var name = Name.Trim();
        parts.Add(name.Length > 0 ? name : "…");
        SummaryText = string.Join(" › ", parts);

        var pendingCount = PendingChain().Count;
        var detail = IsAlbumMode
            ? _sourceFileCount switch {
                null when string.IsNullOrWhiteSpace(SourcePath) => "New album",
                null => "New album · counting photos…",
                1 => "New album · 1 image file to import",
                var n => $"New album · {n} image files to import"
            }
            : "New folder";
        if (pendingCount > 0) {
            detail += pendingCount == 1 ? " · also creates 1 folder" : $" · also creates {pendingCount} folders";
        }
        SummaryDetail = detail;

        ValidationMessage = Validate();
    }

    private string? Validate() {
        var name = Name.Trim();
        if (name.Length > 0 && !Current.IsPending && Current.ContainsName(name)) {
            return $"“{name}” already exists in {(Current == _root ? "the library root" : Current.Name)}.";
        }

        if (!IsAlbumMode) return null;

        if (string.IsNullOrEmpty(_settingsService.Current.LibraryPath)) {
            return "Set a library location in Settings before adding albums.";
        }
        if (Current == _root) {
            return "Albums can't be placed in the library root. Pick or create a folder.";
        }
        if (!string.IsNullOrWhiteSpace(SourcePath) && !Directory.Exists(SourcePath)) {
            return "The source folder doesn't exist.";
        }
        if (_sourceFileCount == 0) {
            return "The source folder has no supported image files.";
        }
        return null;
    }

    private async Task CountSourceFilesAsync(string path) {
        _sourceCountCts?.Cancel();
        var cts = _sourceCountCts = new CancellationTokenSource();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        try {
            var count = await Task.Run(() => Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Count(f => !Path.GetFileName(f).StartsWith('.') && ImageExtensions.Contains(Path.GetExtension(f))), cts.Token);
            if (cts.IsCancellationRequested) return;
            await Dispatcher.UIThread.InvokeAsync(() => {
                _sourceFileCount = count;
                UpdateSummary();
            });
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.Warning(ex, "Could not count images in {Path}", path);
        } catch (OperationCanceledException) {
        }
    }

    /// <summary>
    ///     Pending folders from the outermost to the current one.
    /// </summary>
    private List<LocationFolder> PendingChain() {
        if (Current == _root) return [];
        return Current.Ancestors.Reverse().Append(Current).Where(f => f.IsPending).ToList();
    }

    [RelayCommand]
    private async Task SelectSourcePathAsync() {
        try {
            var topLevel = MainWindow.Instance;
            if (topLevel == null) return;

            var options = new FolderPickerOpenOptions {
                Title = "Select Source Directory",
                AllowMultiple = false
            };

            var startPath = SourcePath;
            if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath)) {
                startPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }
            if (Directory.Exists(startPath)) {
                try {
                    options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startPath);
                } catch (Exception ex) {
                    Log.Debug(ex, "Could not set suggested start location for folder picker");
                }
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count == 0) return;

            SourcePath = folders[0].Path.LocalPath;
            if (string.IsNullOrWhiteSpace(Name)) {
                var suggestedName = Path.GetFileName(SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(suggestedName)) {
                    Name = suggestedName;
                }
            }
        } catch (Exception ex) {
            Log.Error(ex, "Error selecting source path");
        }
    }

    private bool CanCreate() {
        if (IsBusy || HasValidationMessage || string.IsNullOrWhiteSpace(Name)) return false;
        return !IsAlbumMode || !string.IsNullOrWhiteSpace(SourcePath);
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync() {
        IsBusy = true;
        var name = Name.Trim();
        var createdFolders = new List<Folder>();

        // 1. Create the missing folders of the chosen path, outermost first.
        int? parentId;
        try {
            foreach (var pending in PendingChain()) {
                var folder = await _folderService.CreateAsync(pending.Parent?.Id, pending.Name);
                pending.Id = folder.Id;
                pending.IsPending = false;
                (pending.Parent ?? _root).Folders.Add(pending);
                _byId[folder.Id] = pending;
                createdFolders.Add(folder);
            }
            parentId = Current.Id;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to create folder path");
            IsBusy = false;
            if (createdFolders.Count > 0) {
                _onResult(new NewNodeResult(null, createdFolders, null));
            }
            Refresh();
            ValidationMessage = $"Couldn't create the folder path: {ex.Message}";
            return;
        }

        if (parentId.HasValue) {
            _recentLocations.Remember(parentId.Value);
        }

        // 2a. Folder: create it and close.
        if (!IsAlbumMode) {
            try {
                var folder = await _folderService.CreateAsync(parentId, name);
                MainWindow.DialogManager.DismissDialog();
                _onResult(new NewNodeResult(folder, createdFolders, null));
            } catch (Exception ex) {
                Log.Error(ex, "Failed to create folder {Name}", name);
                IsBusy = false;
                if (createdFolders.Count > 0) {
                    _onResult(new NewNodeResult(null, createdFolders, null));
                }
                Refresh();
                ValidationMessage = $"Couldn't create the folder: {ex.Message}";
            }
            return;
        }

        // 2b. Album: close the dialog and import in the background, like the old Add Album dialog.
        var libraryPath = _settingsService.Current.LibraryPath!;
        var sourcePath = SourcePath;
        MainWindow.DialogManager.DismissDialog();

        try {
            var album = await Task.Run(async () => {
                using var scope = _scopeFactory.CreateScope();
                var importCommand = scope.ServiceProvider.GetRequiredService<IImportPicturesCommand>();
                return await importCommand.ExecuteAsync(parentId, name, libraryPath, sourcePath);
            });
            _onResult(new NewNodeResult(album, createdFolders, null));
        } catch (Exception ex) {
            Log.Error(ex, "Import failed");
            _onResult(new NewNodeResult(null, createdFolders, ex.Message));
        }
    }

    [RelayCommand]
    private void Cancel() {
        MainWindow.DialogManager.DismissDialog();
    }
}
