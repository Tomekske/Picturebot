using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Domain.Interfaces;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Main.Views;
using SukiUI.Dialogs;

namespace Main.ViewModels;

public partial class AddAlbumDialogViewModel : ViewModelBase {
    private readonly IAlbumService _albumService;
    private readonly ImportPicturesCommand _importCommand;
    private readonly ISettingsService _settingsService;
    private readonly Action<Album?> _onResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlbumCommand))]
    private string _albumName = string.Empty;

    [ObservableProperty]
    private LocationItem? _selectedParent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlbumCommand))]
    private string _sourcePath = string.Empty;

    public ObservableCollection<LocationItem> Parents { get; } = new();

    public AddAlbumDialogViewModel(
        IAlbumService albumService,
        ImportPicturesCommand importCommand,
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

    [RelayCommand]
    private async Task SelectSourcePathAsync() {
        var topLevel = MainWindow.Instance;
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions {
            Title = "Select Source Directory",
            AllowMultiple = false
        });

        if (folders.Any()) {
            SourcePath = folders[0].Path.LocalPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateAlbum))]
    private async Task CreateAlbumAsync() {
        var libraryPath = _settingsService.Current.LibraryPath;
        if (string.IsNullOrEmpty(libraryPath)) {
            // Handle error: Library path not set
            return;
        }

        var progressVM = new ImportProgressDialogViewModel();
        var progress = new Progress<ImportProgress>(p => progressVM.Update(p));

        // Close the current input dialog and show the progress dialog
        MainWindow.DialogManager.DismissDialog();
        
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new ImportProgressDialog { DataContext = progressVM })
            .TryShow();

        try {
            await _importCommand.ExecuteAsync(SelectedParent?.Id, AlbumName, libraryPath, SourcePath, progress);
            
            // We need to find the created album to return it, or modify ImportCommand to return it.
            // For now, we'll refresh the tree via message in the parent VM.
            _onResult(new Album { Name = AlbumName }); // Placeholder for result
        } catch (Exception ex) {
            Console.WriteLine($"Import failed: {ex.Message}");
            _onResult(null);
        } finally {
            MainWindow.DialogManager.DismissDialog();
        }
    }

    private bool CanCreateAlbum() => !string.IsNullOrWhiteSpace(AlbumName) && !string.IsNullOrWhiteSpace(SourcePath);

    [RelayCommand]
    private void Cancel() {
        _onResult(null);
        MainWindow.DialogManager.DismissDialog();
    }
}
