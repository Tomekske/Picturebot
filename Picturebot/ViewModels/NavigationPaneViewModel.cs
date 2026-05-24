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

public partial class NavigationPaneViewModel : ViewModelBase, 
    IRecipient<NodeCreatedMessage>,
    IRecipient<NodeDeletedMessage> {
    private readonly IAlbumService _albumService;
    private readonly IFolderService _folderService;
    private readonly IImportAlbumsService _importAlbumsService;
    private readonly IImportPicturesCommand _importCommand;
    private readonly INodeService _nodeService;
    private readonly ISettingsService _settingsService;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<NavigationNodeViewModel> _folders = new();

    public NavigationPaneViewModel(
        INodeService nodeService,
        IFolderService folderService,
        IAlbumService albumService,
        IImportAlbumsService importAlbumsService,
        ISettingsService settingsService,
        IImportPicturesCommand importCommand,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory) {
        _nodeService = nodeService;
        _folderService = folderService;
        _albumService = albumService;
        _importAlbumsService = importAlbumsService;
        _settingsService = settingsService;
        _importCommand = importCommand;
        _scopeFactory = scopeFactory;
        _ = LoadFoldersAsync();

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(NodeCreatedMessage message) {
        // Refresh the navigation pane
        _ = LoadFoldersAsync();
    }

    public void Receive(NodeDeletedMessage message) {
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

        var vm = new AddAlbumDialogViewModel(_albumService, _importCommand, _settingsService, _scopeFactory, folders, result => {
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
        var folders = await _folderService.FindAllAsync();

        var vm = new BatchImportAlbumsDialogViewModel(
            _importAlbumsService,
            _settingsService,
            folders,
            async () => {
                // Refresh the tree
                await LoadFoldersAsync();

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent("Batch import of albums has completed.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        );

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new BatchImportAlbumsDialog { DataContext = vm })
            .TryShow();
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
