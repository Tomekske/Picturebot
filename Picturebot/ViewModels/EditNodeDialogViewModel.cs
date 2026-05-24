using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Picturebot.Views;
using Serilog;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class EditNodeDialogViewModel : ViewModelBase {
    private readonly INodeService _nodeService;
    private readonly IFolderService _folderService;
    private readonly Node _nodeToEdit;
    private readonly List<Node> _allNodes;
    private readonly Action<Node?> _onResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _nodeName;

    [ObservableProperty]
    private LocationItem? _selectedParent;

    [ObservableProperty]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    private bool _isCreatingNewFolder;

    public ObservableCollection<LocationItem> Parents { get; } = new();

    public string Title => $"Edit {_nodeToEdit.Type}";

    public EditNodeDialogViewModel(
        INodeService nodeService,
        IFolderService folderService,
        Node nodeToEdit,
        List<Node> allNodes,
        Action<Node?> onResult) {
        _nodeService = nodeService;
        _folderService = folderService;
        _nodeToEdit = nodeToEdit;
        _allNodes = allNodes;
        _onResult = onResult;

        _nodeName = nodeToEdit.Name;

        RefreshParents();

        // Select current parent
        SelectedParent = Parents.FirstOrDefault(p => p.Id == nodeToEdit.ParentId) ?? Parents[0];
    }

    private void RefreshParents() {
        Parents.Clear();
        Parents.Add(new LocationItem { Name = "Library (Root)", Id = null });
        
        PopulateParents(_allNodes, _nodeToEdit.Id);

        Parents.Add(new LocationItem { IsSeparator = true });
        Parents.Add(new LocationItem { Name = "+ Create new folder...", IsAction = true });
    }

    private void PopulateParents(List<Node> nodes, int excludeId, int indent = 1) {
        foreach (var node in nodes) {
            if (node.Type == Domain.Enums.NodeType.Folder) {
                if (node.Id == excludeId) continue;

                Parents.Add(new LocationItem {
                    Name = $"{new string(' ', indent * 2)}{node.Name}",
                    Id = node.Id
                });

                if (node.Children != null) {
                    PopulateParents(node.Children.ToList(), excludeId, indent + 1);
                }
            }
        }
    }

    partial void OnSelectedParentChanged(LocationItem? value) {
        if (value?.IsAction == true) {
            IsCreatingNewFolder = true;
            NewFolderName = string.Empty;
        } else if (value != null && !value.IsSeparator) {
            IsCreatingNewFolder = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmNewFolderAsync() {
        if (string.IsNullOrWhiteSpace(NewFolderName)) return;

        try {
            // Create the new folder under the same parent as the current node
            var newFolder = await _folderService.CreateAsync(_nodeToEdit.ParentId, NewFolderName);
            
            // Refresh the entire nodes list to include the new folder in hierarchy
            var updatedNodes = await _nodeService.LoadHydratedTreeAsync();
            _allNodes.Clear();
            _allNodes.AddRange(updatedNodes);

            RefreshParents();

            // Select the newly created folder
            SelectedParent = Parents.FirstOrDefault(p => p.Id == newFolder.Id);
            IsCreatingNewFolder = false;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to create quick-add folder");
        }
    }

    [RelayCommand]
    private void CancelNewFolder() {
        IsCreatingNewFolder = false;
        // Select Library Root as fallback or previous?
        SelectedParent = Parents.FirstOrDefault(p => p.Id == _nodeToEdit.ParentId) ?? Parents[0];
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() {
        try {
            _nodeToEdit.Name = NodeName;
            _nodeToEdit.ParentId = SelectedParent?.Id;

            await _nodeService.UpdateNodeAsync(_nodeToEdit);
            
            _onResult(_nodeToEdit);
            MainWindow.DialogManager.DismissDialog();
            
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Success")
                .WithContent($"{_nodeToEdit.Type} updated successfully.")
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update {Type}", _nodeToEdit.Type);
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent(ex.Message)
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    private bool CanSave() {
        return !string.IsNullOrWhiteSpace(NodeName);
    }

    [RelayCommand]
    private void Cancel() {
        _onResult(null);
        MainWindow.DialogManager.DismissDialog();
    }
}
