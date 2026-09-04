using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Models;

namespace Picturebot.ViewModels;

public partial class HierarchyNodeViewModel : ViewModelBase {
    private readonly Action<HierarchyNodeViewModel>? _onCommit;
    private readonly Action<HierarchyNodeViewModel>? _onCancel;

    public HierarchyNode Model { get; }

    public Guid NodeId => Model.NodeId;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private Guid? _tagId;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isNewNode;

    [ObservableProperty]
    private string _editingName = string.Empty;

    public bool IsNewUncommitted { get; set; }

    public HierarchyNodeViewModel? Parent { get; set; }

    public ObservableCollection<HierarchyNodeViewModel> Children { get; } = new();

    public HierarchyNodeViewModel(HierarchyNode model, HierarchyNodeViewModel? parent = null, Action<HierarchyNodeViewModel>? onCommit = null, Action<HierarchyNodeViewModel>? onCancel = null) {
        Model = model;
        _name = model.Name;
        _tagId = model.TagId;
        _editingName = model.Name;
        Parent = parent;
        _onCommit = onCommit;
        _onCancel = onCancel;

        foreach (var child in model.Children) {
            Children.Add(new HierarchyNodeViewModel(child, this, onCommit, onCancel));
        }
    }

    public HierarchyNodeViewModel(string name, Guid? tagId = null, HierarchyNodeViewModel? parent = null, Action<HierarchyNodeViewModel>? onCommit = null, Action<HierarchyNodeViewModel>? onCancel = null) {
        Model = new HierarchyNode {
            NodeId = Guid.NewGuid(),
            Name = name,
            TagId = tagId
        };
        _name = name;
        _tagId = tagId;
        _editingName = name;
        Parent = parent;
        _onCommit = onCommit;
        _onCancel = onCancel;
    }

    partial void OnNameChanged(string value) {
        Model.Name = value;
    }

    partial void OnTagIdChanged(Guid? value) {
        Model.TagId = value;
    }

    [RelayCommand]
    public void StartEdit() {
        EditingName = Name;
        IsNewNode = false;
        IsEditing = true;
    }

    [RelayCommand]
    public void CommitEdit() {
        _onCommit?.Invoke(this);
    }

    [RelayCommand]
    public void CancelEdit() {
        _onCancel?.Invoke(this);
    }

    public string GetXmpPath() {
        var parentPath = Parent?.GetXmpPath();
        return string.IsNullOrEmpty(parentPath) ? Name : $"{parentPath}|{Name}";
    }

    public string GetDisplayBreadcrumb() {
        var parentPath = Parent?.GetDisplayBreadcrumb();
        return string.IsNullOrEmpty(parentPath) ? Name : $"{parentPath} › {Name}";
    }

    public HierarchyNode ToModel() {
        var node = new HierarchyNode {
            NodeId = NodeId,
            Name = Name,
            TagId = TagId
        };
        foreach (var child in Children) {
            node.Children.Add(child.ToModel());
        }
        return node;
    }
}
