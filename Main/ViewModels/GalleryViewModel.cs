using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Main.Views;
using SukiUI.Dialogs;

namespace Main.ViewModels;

public partial class GalleryViewModel : ViewModelBase {
    private readonly IFolderService _folderService;
    private readonly INodeService _nodeService;

    public GalleryViewModel(INodeService nodeService, IFolderService folderService) {
        _nodeService = nodeService;
        _folderService = folderService;
    }

    [RelayCommand]
    public async Task OpenCreateFolderDialogAsync() {
        // 1. Fetch existing folders for the parent selection list
        var allNodes = await _nodeService.LoadHydratedTreeAsync();
        var folders = FlattenFolders(allNodes);

        // 2. Initialize the ViewModel
        var vm = new CreateFolderDialogViewModel(_folderService, result => {
            if (result != null) {
                // Refresh logic here
                Console.WriteLine($"Folder created: {result.Name}");
                RefreshGallery();
            }
        }, folders);

        // 3. Trigger SukiDialog
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new CreateFolderDialog { DataContext = vm })
            .TryShow();
    }

    private List<Folder> FlattenFolders(IEnumerable<Node> nodes) {
        var folders = new List<Folder>();
        foreach (var node in nodes) {
            if (node is Folder folder) {
                folders.Add(folder);
                if (folder.Children != null) {
                    folders.AddRange(FlattenFolders(folder.Children));
                }
            }
        }

        return folders;
    }

    private void RefreshGallery() {
        // Implement gallery refresh logic
    }
}
