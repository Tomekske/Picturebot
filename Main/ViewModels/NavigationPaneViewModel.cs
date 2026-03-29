using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Main.Messages;
using Main.Views;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Main.ViewModels;

public partial class NavigationPaneViewModel : ViewModelBase, IRecipient<FolderCreatedMessage> {
    private readonly IFolderService _folderService;
    private readonly INodeService _nodeService;

    [ObservableProperty]
    private ObservableCollection<NavigationNodeViewModel> _folders = new();

    public NavigationPaneViewModel(INodeService nodeService, IFolderService folderService) {
        _nodeService = nodeService;
        _folderService = folderService;
        _ = LoadFoldersAsync();

        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(FolderCreatedMessage message) {
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
                WeakReferenceMessenger.Default.Send(new FolderCreatedMessage(result));

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
