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
                .Queue();
            return;
        }

        var progressVM = new ImportProgressDialogViewModel();
        var progress = new Progress<ImportBatchProgress>(p => progressVM.Update(p));

        MainWindow.DialogManager.DismissDialog();

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new ImportProgressDialog { DataContext = progressVM })
            .TryShow();

        try {
            await Task.Run(async () => {
                await _importAlbumsService.ImportRecursiveAsync(SelectedParent?.Id, SourcePath, libraryPath, progress);
            });

            Log.Information("Batch import completed for: {sourcePath}", SourcePath);
            _onCompleted();
        } catch (Exception ex) {
            Log.Error(ex, "Batch import failed");
        } finally {
            MainWindow.DialogManager.DismissDialog();
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
