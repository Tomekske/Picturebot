using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Domain.Interfaces;
using Domain.TimeZones;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Picturebot.Services;
using Picturebot.Views;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class BatchImportAlbumsDialogViewModel : ViewModelBase {
    private readonly IImportAlbumsService _importAlbumsService;
    private readonly Action _onCompleted;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private LocationItem? _selectedParent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _selectedParentPath = "Library";

    /// <summary>
    ///     The zone for every imported album and folder; null inherits it from the folders above.
    /// </summary>
    [ObservableProperty]
    private string? _timeZoneId;

    [ObservableProperty]
    private string? _inheritedTimeZoneId;

    [ObservableProperty]
    private string _timeZoneHint = string.Empty;

    private TimeZoneSource? _inheritedTimeZone;

    public BatchImportAlbumsDialogViewModel(
        IImportAlbumsService importAlbumsService,
        ISettingsService settingsService,
        List<Folder> existingFolders,
        Action onCompleted,
        string initialSourcePath = "") {
        _importAlbumsService = importAlbumsService;
        _settingsService = settingsService;
        _onCompleted = onCompleted;
        _sourcePath = initialSourcePath;

        FolderTree = FolderNodeViewModel.BuildFolderTree(existingFolders, OnFolderSelected);
        SelectedParent = new LocationItem { Name = "Library", Id = null };
        UpdateInheritedTimeZone(null);
    }

    public ObservableCollection<FolderNodeViewModel> FolderTree { get; }

    private void OnFolderSelected(FolderNodeViewModel node) {
        SelectedParent = new LocationItem { Name = node.Name, Id = node.Id };
        SelectedParentPath = node.FullPath;
        UpdateInheritedTimeZone(node);
    }

    partial void OnTimeZoneIdChanged(string? value) => UpdateTimeZoneHint();

    private void UpdateInheritedTimeZone(FolderNodeViewModel? parent) {
        var source = parent?.NearestWithTimeZone;
        _inheritedTimeZone = source is { Id: not null, TimeZoneId: not null }
            ? new TimeZoneSource(source.TimeZoneId, source.Id.Value, source.Name)
            : null;
        InheritedTimeZoneId = ViewModels.TimeZoneHint.InheritedZoneId(_inheritedTimeZone, _settingsService.Current.DefaultTimeZoneId);
        UpdateTimeZoneHint();
    }

    private void UpdateTimeZoneHint() {
        TimeZoneHint = ViewModels.TimeZoneHint.Describe(TimeZoneId, _inheritedTimeZone,
            _settingsService.Current.DefaultTimeZoneId, isFolder: true);
    }

    [RelayCommand]
    private void ResetToLibrary() {
        DeselectAll(FolderTree);
        SelectedParent = new LocationItem { Name = "Library", Id = null };
        SelectedParentPath = "Library";
        UpdateInheritedTimeZone(null);
    }

    private void DeselectAll(IEnumerable<FolderNodeViewModel> nodes) {
        foreach (var node in nodes) {
            node.IsSelected = false;
            DeselectAll(node.Children);
        }
    }

    [RelayCommand]
    private async Task SelectSourcePathAsync() {
        try {
            var topLevel = MainWindow.Instance;
            if (topLevel == null) {
                return;
            }

            var options = new FolderPickerOpenOptions {
                Title = "Select Root Directory for Import",
                AllowMultiple = false
            };

            // Try to set a valid starting location to avoid platform-specific initialization crashes
            var startPath = SourcePath;
            if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath)) {
                startPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (!Directory.Exists(startPath)) {
                    startPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
            }

            if (Directory.Exists(startPath)) {
                try {
                    options.SuggestedStartLocation =
                        await topLevel.StorageProvider.TryGetFolderFromPathAsync(startPath);
                } catch (Exception ex) {
                    Log.Debug(ex, "Could not set suggested start location for folder picker");
                }
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);

            if (folders.Any()) {
                SourcePath = folders[0].Path.LocalPath;
            }
        } catch (Exception ex) {
            Log.Error(ex, "Error selecting source path");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task StartImportAsync() {
        var libraryPath = _settingsService.Current.LibraryPath;
        if (string.IsNullOrEmpty(libraryPath)) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent("Library path is not set in settings.")
                .Dismiss().ByClicking()
                .Queue();
            return;
        }

        MainWindow.DialogManager.DismissDialog();
        var parentId = SelectedParent?.Id;
        var sourcePath = SourcePath;
        var timeZoneId = ViewModels.TimeZoneHint.Normalize(TimeZoneId, _inheritedTimeZone, _settingsService.Current.DefaultTimeZoneId);
        var feedback = ImportFeedback.Start(Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

        try {
            // Start the batch import in the background. Recursive import creates album nodes (copy phase).
            _ = Task.Run(async () => {
                try {
                    await _importAlbumsService.ImportRecursiveAsync(parentId, sourcePath, libraryPath, feedback, timeZoneId);
                    feedback.Completed();
                    Dispatcher.UIThread.Post(() => _onCompleted());
                } catch (Exception ex) {
                    Log.Error(ex, "Batch import failed");
                    feedback.Failed();
                }
            });

            Log.Information("Batch import started for: {sourcePath}", SourcePath);
        } catch (Exception ex) {
            Log.Error(ex, "Batch import failed");
        }
    }

    private bool CanStartImport() {
        return !string.IsNullOrWhiteSpace(SourcePath);
    }

    [RelayCommand]
    private void Cancel() {
        MainWindow.DialogManager.DismissDialog();
    }
}
