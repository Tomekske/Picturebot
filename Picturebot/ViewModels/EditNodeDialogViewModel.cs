using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Domain.Enums;
using Domain.TimeZones;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeZoneHint))]
    private string? _timeZoneId;

    // The zone used when this node has none: the nearest folder's above it, else the default
    [ObservableProperty]
    private string? _inheritedTimeZoneId;

    private TimeZoneSource? _inheritedTimeZone;
    private readonly string? _defaultTimeZoneId;

    public ObservableCollection<LocationItem> Parents { get; } = new();

    public string Title => $"Edit {_nodeToEdit.Type}";

    public bool HasTimeZone => _nodeToEdit is Album or Folder;

    public string TimeZoneHint => ViewModels.TimeZoneHint.Describe(TimeZoneId, _inheritedTimeZone, _defaultTimeZoneId,
        isFolder: _nodeToEdit is Folder);

    public EditNodeDialogViewModel(
        INodeService nodeService,
        IFolderService folderService,
        Node nodeToEdit,
        List<Node> allNodes,
        Action<Node?> onResult,
        string? defaultTimeZoneId = null) {
        _nodeService = nodeService;
        _folderService = folderService;
        _nodeToEdit = nodeToEdit;
        _allNodes = allNodes;
        _onResult = onResult;
        _defaultTimeZoneId = defaultTimeZoneId;

        _nodeName = nodeToEdit.Name;
        _timeZoneId = nodeToEdit switch {
            Album album => album.TimeZoneId,
            Folder folder => folder.TimeZoneId,
            _ => null
        };

        RefreshParents();

        // Select current parent
        SelectedParent = Parents.FirstOrDefault(p => p.Id == nodeToEdit.ParentId) ?? Parents[0];
    }

    /// <summary>
    ///     Looks up what the node inherits under the selected parent (it changes when the node is moved).
    /// </summary>
    private async Task LoadInheritedTimeZoneAsync(int? parentId) {
        try {
            _inheritedTimeZone = parentId.HasValue ? await _nodeService.ResolveTimeZoneAsync(parentId.Value) : null;
        } catch (Exception ex) {
            Log.Warning(ex, "Could not resolve the inherited time zone of {Name}", _nodeToEdit.Name);
            _inheritedTimeZone = null;
        }
        InheritedTimeZoneId = ViewModels.TimeZoneHint.InheritedZoneId(_inheritedTimeZone, _defaultTimeZoneId);
        OnPropertyChanged(nameof(TimeZoneHint));
    }

    partial void OnTimeZoneIdChanged(string? value) => OnPropertyChanged(nameof(TimeZoneHint));

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
            _ = LoadInheritedTimeZoneAsync(value.Id);
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
            // The same zone as the inherited one is stored as "not set", so it keeps following the folder above
            var ownTimeZoneId = ViewModels.TimeZoneHint.Normalize(TimeZoneId, _inheritedTimeZone, _defaultTimeZoneId);
            switch (_nodeToEdit) {
                case Album album:
                    album.TimeZoneId = ownTimeZoneId;
                    break;
                case Folder folder:
                    folder.TimeZoneId = ownTimeZoneId;
                    break;
            }

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
