using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Models;

namespace Picturebot.ViewModels;

public partial class HierarchyNodeViewModel : ViewModelBase {
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

    public HierarchyNodeViewModel? Parent { get; set; }

    public ObservableCollection<HierarchyNodeViewModel> Children { get; } = new();

    public HierarchyNodeViewModel(HierarchyNode model, HierarchyNodeViewModel? parent = null) {
        Model = model;
        _name = model.Name;
        _tagId = model.TagId;
        Parent = parent;

        foreach (var child in model.Children) {
            Children.Add(new HierarchyNodeViewModel(child, this));
        }
    }

    public HierarchyNodeViewModel(string name, Guid? tagId = null, HierarchyNodeViewModel? parent = null) {
        Model = new HierarchyNode {
            NodeId = Guid.NewGuid(),
            Name = name,
            TagId = tagId
        };
        _name = name;
        _tagId = tagId;
        Parent = parent;
    }

    partial void OnNameChanged(string value) {
        Model.Name = value;
    }

    partial void OnTagIdChanged(Guid? value) {
        Model.TagId = value;
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
