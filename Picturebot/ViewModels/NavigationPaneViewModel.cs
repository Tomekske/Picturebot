using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Domain.Interfaces;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Picturebot.Messages;
using Picturebot.Views;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class NavigationPaneViewModel : ViewModelBase, IRecipient<NodeCreatedMessage> {
    private readonly IAlbumService _albumService;
    private readonly IFolderService _folderService;
    private readonly IImportAlbumsService _importAlbumsService;
    private readonly IImportPicturesCommand _importCommand;
    private readonly INodeService _nodeService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<NavigationNodeViewModel> _folders = new();

    public NavigationPaneViewModel(
        INodeService nodeService,
        IFolderService folderService,
        IAlbumService albumService,
        IImportAlbumsService importAlbumsService,
        ISettingsService settingsService,
        IImportPicturesCommand importCommand) {
        _nodeService = nodeService;
        _folderService = folderService;
        _albumService = albumService;
        _importAlbumsService = importAlbumsService;
        _settingsService = settingsService;
        _importCommand = importCommand;
        _ = LoadFoldersAsync();

        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(NodeCreatedMessage message) {
        // Refresh the navigation pane
        _ = LoadFoldersAsync();
    }

    [RelayCommand]
    public async Task OpenCreateFolderDialogAsync() {
        var folders = await _folderService.FindAllAsync();

        var vm = new CreateFolderDialogViewModel(_folderService, result => {
            if (result != null) {
                Log.Information("Folder created: {result}", result.Name);

                // Broadcast creation to refresh the tree
                WeakReferenceMessenger.Default.Send(new NodeCreatedMessage(result));

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent($"Folder '{result.Name}' has been created.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }, folders);

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new CreateFolderDialog { DataContext = vm })
            .TryShow();
    }

    [RelayCommand]
    public async Task OpenAddAlbumDialogAsync() {
        var folders = await _folderService.FindAllAsync();

        var vm = new AddAlbumDialogViewModel(_albumService, _importCommand, _settingsService, folders, result => {
            if (result != null) {
                Log.Information("Album creation process started/finished for: {result}", result.Name);

                // Broadcast creation to refresh the tree
                WeakReferenceMessenger.Default.Send(new NodeCreatedMessage(result));

                // Automatically navigate to the new album
                WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(result));

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent($"Album '{result.Name}' import has completed.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        });

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new AddAlbumDialog { DataContext = vm })
            .TryShow();
    }

    [RelayCommand]
    public async Task OpenImportAlbumsDialogAsync() {
        try {
            var topLevel = MainWindow.Instance;
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                Title = "Select Root Directory for Import",
                AllowMultiple = false
            });

            if (!folders.Any()) return;

            var sourcePath = folders[0].Path.LocalPath;
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

            MainWindow.DialogManager.CreateDialog()
                .WithContent(new ImportProgressDialog { DataContext = progressVM })
                .TryShow();

            try {
                await Task.Run(async () => {
                    await _importAlbumsService.ImportRecursiveAsync(null, sourcePath, libraryPath, progress);
                });

                Log.Information("Batch import completed for: {sourcePath}", sourcePath);

                // Refresh the tree
                await LoadFoldersAsync();

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent("Batch import of albums has completed.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            } catch (Exception ex) {
                Log.Error(ex, "Batch import failed");
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Error")
                    .WithContent($"Batch import failed: {ex.Message}")
                    .Queue();
            } finally {
                MainWindow.DialogManager.DismissDialog();
            }
        } catch (Exception ex) {
            Log.Error(ex, "Error starting batch import");
        }
    }

    public async Task LoadFoldersAsync() {
        var roots = await _nodeService.LoadHydratedTreeAsync();

        await Dispatcher.UIThread.InvokeAsync(() => {
            Folders.Clear();
            foreach (var root in roots) {
                if (root is Folder || root is Album) {
                    Folders.Add(new NavigationNodeViewModel(root));
                }
            }
        });
    }
}
