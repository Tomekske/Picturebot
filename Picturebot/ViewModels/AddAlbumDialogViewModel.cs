using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Domain.Interfaces;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Picturebot.Views;
using Serilog;
using SukiUI.Dialogs;

namespace Picturebot.ViewModels;

public partial class AddAlbumDialogViewModel : ViewModelBase {
    private readonly IAlbumService _albumService;
    private readonly IImportPicturesCommand _importCommand;
    private readonly Action<Album?> _onResult;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlbumCommand))]
    private string _albumName = string.Empty;

    [ObservableProperty]
    private LocationItem? _selectedParent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlbumCommand))]
    private string _sourcePath = string.Empty;

    public AddAlbumDialogViewModel(
        IAlbumService albumService,
        IImportPicturesCommand importCommand,
        ISettingsService settingsService,
        List<Folder> existingFolders,
        Action<Album?> onResult) {
        _albumService = albumService;
        _importCommand = importCommand;
        _settingsService = settingsService;
        _onResult = onResult;

        Parents.Add(new LocationItem { Name = "Library", Id = null });
        foreach (var f in existingFolders) {
            Parents.Add(new LocationItem { Name = f.Name, Id = f.Id });
        }

        SelectedParent = Parents[0];
    }

    public ObservableCollection<LocationItem> Parents { get; } = new();

    [RelayCommand]
    private async Task SelectSourcePathAsync() {
        try {
            var topLevel = MainWindow.Instance;
            if (topLevel == null) {
                return;
            }

            var options = new FolderPickerOpenOptions {
                Title = "Select Source Directory",
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
                    options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startPath);
                } catch (Exception ex) {
                    Log.Debug(ex, "Could not set suggested start location for folder picker");
                }
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);

            if (folders.Any()) {
                var selectedPath = folders[0].Path.LocalPath;
                SourcePath = selectedPath;

                // Auto-populate Album Name if it is currently blank.
                if (string.IsNullOrWhiteSpace(AlbumName)) {
                    // Trim trailing slashes to ensure GetFileName safely extracts the leaf folder
                    var cleanPath = selectedPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    );

                    var suggestedName = Path.GetFileName(cleanPath);

                    if (!string.IsNullOrWhiteSpace(suggestedName)) {
                        AlbumName = suggestedName;
                    }
                }
            }
        } catch (Exception ex) {
            Log.Error(ex, "Error selecting source path");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateAlbum))]
    private async Task CreateAlbumAsync() {
        var libraryPath = _settingsService.Current.LibraryPath;
        if (string.IsNullOrEmpty(libraryPath)) {
            // Handle error: Library path not set
            return;
        }

        // Close the current input dialog immediately
        MainWindow.DialogManager.DismissDialog();

        try {
            // Start the import in the background. ExecuteAsync only does copying now.
            var album = await _importCommand.ExecuteAsync(SelectedParent?.Id, AlbumName, libraryPath, SourcePath);

            _onResult(album);
        } catch (Exception ex) {
            Log.Error(ex, "Import failed");
            _onResult(null);
        }
    }

    private bool CanCreateAlbum() {
        return !string.IsNullOrWhiteSpace(AlbumName) && !string.IsNullOrWhiteSpace(SourcePath);
    }

    [RelayCommand]
    private void Cancel() {
        _onResult(null);
        MainWindow.DialogManager.DismissDialog();
    }
}
