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
using Domain.TimeZones;
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
public record NewNodeResult(Node? Node, IReadOnlyList<Folder> CreatedFolders, string? Error, bool IsBatchImport = false);

/// <summary>
///     The combined New Album / New Folder dialog: one location picker with a path bar, one level at a time,
///     search across all folder paths, recent locations, and creating missing folders in one go.
/// </summary>
public partial class NewNodeDialogViewModel : ViewModelBase {
    private static readonly char[] PathSeparators = ['›', '/', '\\', '>'];
    private static readonly HashSet<string> ImageExtensions =
        new(SupportedImageExtensions.AllExtensions, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> RawExtensions =
        new(SupportedImageExtensions.RawExtensions, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<int, LocationFolder> _byId = new();
    private readonly IFolderService _folderService;
    private readonly Action<NewNodeResult> _onResult;
    private readonly IRecentLocationsService _recentLocations;
    private readonly LocationFolder _root = new(null, "Library", null);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _sourceCountCts;
    private SourceCounts? _sourceCounts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private LocationFolder _current;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isAlbumMode;

    /// <summary>
    ///     Recursive import: every subfolder of the source becomes a folder or, when it holds pictures, an album.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isImportMode;

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

    /// <summary>
    ///     The zone chosen for the new album or folder; null inherits it from the folders above.
    /// </summary>
    [ObservableProperty]
    private string? _timeZoneId;

    [ObservableProperty]
    private string? _inheritedTimeZoneId;

    [ObservableProperty]
    private string _timeZoneHint = string.Empty;

    private TimeZoneSource? _inheritedTimeZone;

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
        UpdateInheritedTimeZone();
    }

    public ObservableCollection<PathSegment> PathSegments { get; } = new();
    public ObservableCollection<LocationRow> Rows { get; } = new();

    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);
    public bool HasRows => Rows.Count > 0;
    public bool IsFolderMode => !IsAlbumMode && !IsImportMode;
    public bool HasSource => IsAlbumMode || IsImportMode;
    public string Title => IsImportMode ? "Import Albums" : IsAlbumMode ? "New Album" : "New Folder";
    public string CreateButtonText => IsImportMode ? "Import Albums" : IsAlbumMode ? "Create Album" : "Create Folder";
    public string NameWatermark => IsAlbumMode ? "e.g. Kyoto Day 2" : "e.g. Kyoto";
    public string SourceLabel => IsImportMode ? "ROOT SOURCE FOLDER" : "SOURCE FOLDER";
    public string SourceWatermark => IsImportMode ? "Folder with the albums to import" : "Folder with the photos to import";

    /// <summary>
    ///     The folder that the recursive import creates at the chosen location, named after the source folder.
    /// </summary>
    private string ImportRootName =>
        Path.GetFileName(SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private void AddNode(Node node, LocationFolder parent) {
        switch (node) {
            case Folder folder: {
                var location = new LocationFolder(folder.Id, folder.Name, parent == _root ? null : parent) {
                    TimeZoneId = folder.TimeZoneId
                };
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

    partial void OnCurrentChanged(LocationFolder value) {
        Refresh();
        UpdateInheritedTimeZone();
    }

    partial void OnTimeZoneIdChanged(string? value) => UpdateTimeZoneHint();

    /// <summary>
    ///     The zone of the nearest folder up the path that has one (Japan › Kyoto › here: Japan's zone).
    /// </summary>
    private void UpdateInheritedTimeZone() {
        var source = Current == _root
            ? null
            : Current.Ancestors.Prepend(Current).FirstOrDefault(f => f.Id.HasValue && !string.IsNullOrEmpty(f.TimeZoneId));
        _inheritedTimeZone = source == null ? null : new TimeZoneSource(source.TimeZoneId!, source.Id!.Value, source.Name);
        InheritedTimeZoneId = ViewModels.TimeZoneHint.InheritedZoneId(_inheritedTimeZone, _settingsService.Current.DefaultTimeZoneId);
        UpdateTimeZoneHint();
    }

    private void UpdateTimeZoneHint() {
        TimeZoneHint = ViewModels.TimeZoneHint.Describe(TimeZoneId, _inheritedTimeZone,
            _settingsService.Current.DefaultTimeZoneId, isFolder: !IsAlbumMode);
    }
    partial void OnQueryChanged(string value) => RebuildRows();
    partial void OnNameChanged(string value) => UpdateSummary();

    partial void OnIsAlbumModeChanged(bool value) => OnModeChanged();

    partial void OnIsImportModeChanged(bool value) => OnModeChanged();

    private void OnModeChanged() {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CreateButtonText));
        OnPropertyChanged(nameof(NameWatermark));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(SourceWatermark));
        OnPropertyChanged(nameof(IsFolderMode));
        OnPropertyChanged(nameof(HasSource));
        UpdateSummary();
        UpdateTimeZoneHint();
    }

    partial void OnSourcePathChanged(string value) {
        _sourceCounts = null;
        UpdateSummary();
        CreateCommand.NotifyCanExecuteChanged();
        _ = CountSourceFilesAsync(value);
    }

    [RelayCommand]
    private void SetMode(string mode) {
        IsImportMode = mode == "Import";
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
        var name = IsImportMode ? ImportRootName : Name.Trim();
        parts.Add(name.Length > 0 ? name : "…");
        SummaryText = string.Join(" › ", parts);

        var pendingCount = PendingChain().Count;
        var detail = IsImportMode
            ? "Imports every subfolder: folders, and albums where a folder holds pictures"
            : IsAlbumMode
            ? _sourceCounts switch {
                null when string.IsNullOrWhiteSpace(SourcePath) => "New album",
                null => "New album · counting photos…",
                var counts => $"New album · {counts.Describe()}"
            }
            : "New folder";
        if (pendingCount > 0) {
            detail += pendingCount == 1 ? " · also creates 1 folder" : $" · also creates {pendingCount} folders";
        }
        SummaryDetail = detail;

        ValidationMessage = Validate();
    }

    private string? Validate() {
        var name = IsImportMode ? ImportRootName : Name.Trim();
        if (name.Length > 0 && !Current.IsPending && Current.ContainsName(name)) {
            return $"“{name}” already exists in {(Current == _root ? "the library root" : Current.Name)}.";
        }

        if (IsImportMode) {
            if (string.IsNullOrEmpty(_settingsService.Current.LibraryPath)) {
                return "Set a library location in Settings before importing albums.";
            }
            if (!string.IsNullOrWhiteSpace(SourcePath) && !Directory.Exists(SourcePath)) {
                return "The source folder doesn't exist.";
            }
            return null;
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
        if (_sourceCounts?.Pictures == 0) {
            return "The source folder has no supported image files.";
        }
        if (_sourceCounts is { Unpaired.Count: > 0 } counts) {
            var examples = string.Join(", ", counts.Unpaired.Take(3));
            var more = counts.Unpaired.Count > 3 ? $" and {counts.Unpaired.Count - 3} more" : string.Empty;
            return counts.Unpaired.Count == 1
                ? $"Every picture needs both a RAW and a JPG. This file has no partner: {examples}."
                : $"Every picture needs both a RAW and a JPG. {counts.Unpaired.Count} files have no partner: {examples}{more}.";
        }
        return null;
    }

    private async Task CountSourceFilesAsync(string path) {
        _sourceCountCts?.Cancel();
        var cts = _sourceCountCts = new CancellationTokenSource();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        try {
            var counts = await Task.Run(() => {
                var files = Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => !Path.GetFileName(f).StartsWith('.') && ImageExtensions.Contains(Path.GetExtension(f)))
                    .ToList();
                // Like the import: files with the same name (a RAW and its JPG) are one picture.
                var groups = files.GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase).ToList();
                var unpaired = groups
                    .Where(g => !g.Any(f => RawExtensions.Contains(Path.GetExtension(f))) ||
                                g.All(f => RawExtensions.Contains(Path.GetExtension(f))))
                    .SelectMany(g => g.Select(Path.GetFileName))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new SourceCounts(groups.Count, unpaired);
            }, cts.Token);
            if (cts.IsCancellationRequested) return;
            await Dispatcher.UIThread.InvokeAsync(() => {
                _sourceCounts = counts;
                UpdateSummary();
                CreateCommand.NotifyCanExecuteChanged();
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

    /// <summary>
    ///     Where Browse opens: the current or last picked source folder. Walks up when that folder no longer exists;
    ///     falls back to Pictures.
    /// </summary>
    private string? GetBrowseStartPath() {
        foreach (var picked in new[] { SourcePath, _recentLocations.LastSourceFolder }) {
            if (string.IsNullOrWhiteSpace(picked)) continue;

            var folder = picked.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) {
                folder = Path.GetDirectoryName(folder);
            }
            if (!string.IsNullOrEmpty(folder)) return folder;
        }

        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Directory.Exists(pictures) ? pictures : null;
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

            var startPath = GetBrowseStartPath();
            if (startPath != null) {
                try {
                    options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startPath);
                } catch (Exception ex) {
                    Log.Debug(ex, "Could not set suggested start location for folder picker");
                }
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count == 0) return;

            SourcePath = folders[0].Path.LocalPath;
            _recentLocations.LastSourceFolder = SourcePath;
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
        if (IsBusy || HasValidationMessage) return false;
        if (IsImportMode) return !string.IsNullOrWhiteSpace(SourcePath);
        if (string.IsNullOrWhiteSpace(Name)) return false;
        // An album also waits for the source count, which is where the RAW/JPG pairing is checked.
        return !IsAlbumMode || (!string.IsNullOrWhiteSpace(SourcePath) && _sourceCounts != null);
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

        // 2c. Recursive import: close the dialog and import every subfolder in the background.
        if (IsImportMode) {
            var importLibraryPath = _settingsService.Current.LibraryPath!;
            var rootPath = SourcePath;
            var importTimeZoneId = OwnTimeZoneId();
            _recentLocations.LastSourceFolder = rootPath;
            MainWindow.DialogManager.DismissDialog();
            var importFeedback = ImportFeedback.Start(ImportRootName);

            try {
                await Task.Run(async () => {
                    using var scope = _scopeFactory.CreateScope();
                    var importService = scope.ServiceProvider.GetRequiredService<IImportAlbumsService>();
                    await importService.ImportRecursiveAsync(parentId, rootPath, importLibraryPath, importFeedback, importTimeZoneId);
                });
                importFeedback.Completed();
                _onResult(new NewNodeResult(null, createdFolders, null, IsBatchImport: true));
            } catch (Exception ex) {
                Log.Error(ex, "Batch import failed");
                importFeedback.Failed();
                _onResult(new NewNodeResult(null, createdFolders, ex.Message, IsBatchImport: true));
            }
            return;
        }

        // 2a. Folder: create it and close.
        if (!IsAlbumMode) {
            try {
                var folder = await _folderService.CreateAsync(parentId, name, OwnTimeZoneId());
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
        var timeZoneId = OwnTimeZoneId();
        _recentLocations.LastSourceFolder = sourcePath;
        MainWindow.DialogManager.DismissDialog();
        var feedback = ImportFeedback.Start(name);

        try {
            var album = await Task.Run(async () => {
                using var scope = _scopeFactory.CreateScope();
                var importCommand = scope.ServiceProvider.GetRequiredService<IImportPicturesCommand>();
                return await importCommand.ExecuteAsync(parentId, name, libraryPath, sourcePath, feedback, timeZoneId);
            });
            feedback.Completed();
            _onResult(new NewNodeResult(album, createdFolders, null));
        } catch (Exception ex) {
            Log.Error(ex, "Import failed");
            feedback.Failed();
            _onResult(new NewNodeResult(null, createdFolders, ex.Message));
        }
    }

    /// <summary>
    ///     The chosen zone, or null when it's the same as the inherited one so the new node keeps following it.
    /// </summary>
    private string? OwnTimeZoneId() {
        return ViewModels.TimeZoneHint.Normalize(TimeZoneId, _inheritedTimeZone, _settingsService.Current.DefaultTimeZoneId);
    }

    [RelayCommand]
    private void Cancel() {
        MainWindow.DialogManager.DismissDialog();
    }

    /// <param name="Pictures">Files grouped by name, as the import groups them.</param>
    /// <param name="Unpaired">Files whose RAW or JPG partner is missing.</param>
    private record SourceCounts(int Pictures, IReadOnlyList<string> Unpaired) {
        public string Describe() => Pictures switch {
            0 => "no pictures to import",
            1 => "1 picture to import (RAW + JPG)",
            _ => $"{Pictures} pictures to import (RAW + JPG)"
        };
    }
}
