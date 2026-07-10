using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Picturebot.Views;

namespace Picturebot.ViewModels;

public partial class CreateFolderDialogViewModel : ViewModelBase {
    private readonly IFolderService _folderService;
    private readonly Action<Folder?> _onResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateFolderCommand))]
    private string _folderName = string.Empty;

    [ObservableProperty]
    private LocationItem? _selectedParent;

    [ObservableProperty]
    private string _selectedParentPath = "Library";

    public CreateFolderDialogViewModel(IFolderService folderService, Action<Folder?> onResult,
        List<Folder> existingFolders) {
        _folderService = folderService;
        _onResult = onResult;

        FolderTree = FolderNodeViewModel.BuildFolderTree(existingFolders, OnFolderSelected);
        SelectedParent = new LocationItem { Name = "Library", Id = null };
    }

    public ObservableCollection<FolderNodeViewModel> FolderTree { get; }

    private void OnFolderSelected(FolderNodeViewModel node) {
        SelectedParent = new LocationItem { Name = node.Name, Id = node.Id };
        SelectedParentPath = node.FullPath;
    }

    [RelayCommand]
    private void ResetToLibrary() {
        DeselectAll(FolderTree);
        SelectedParent = new LocationItem { Name = "Library", Id = null };
        SelectedParentPath = "Library";
    }

    private void DeselectAll(IEnumerable<FolderNodeViewModel> nodes) {
        foreach (var node in nodes) {
            node.IsSelected = false;
            DeselectAll(node.Children);
        }
    }



    [RelayCommand(CanExecute = nameof(CanCreateFolder))]
    private async Task CreateFolderAsync() {
        try {
            var createdFolder = await _folderService.CreateAsync(SelectedParent?.Id, FolderName);
            _onResult(createdFolder);
            MainWindow.DialogManager.DismissDialog();
        } catch (Exception ex) {
            // Handle error (e.g., duplicate name)
            Console.WriteLine(ex.Message);
        }
    }

    private bool CanCreateFolder() {
        return !string.IsNullOrWhiteSpace(FolderName);
    }

    [RelayCommand]
    private void Cancel() {
        _onResult(null);
        MainWindow.DialogManager.DismissDialog();
    }
}
