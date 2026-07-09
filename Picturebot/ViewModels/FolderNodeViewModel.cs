using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Database.Domain.Entities;

namespace Picturebot.ViewModels;

public partial class FolderNodeViewModel : ViewModelBase {
    private readonly Folder? _folder;
    private readonly Action<FolderNodeViewModel>? _onSelectionChanged;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    public FolderNodeViewModel(Folder? folder, string displayName, Action<FolderNodeViewModel>? onSelectionChanged) {
        _folder = folder;
        Id = folder?.Id;
        Name = displayName;
        _onSelectionChanged = onSelectionChanged;
    }

    public int? Id { get; }
    public string Name { get; }
    public FolderNodeViewModel? ParentNode { get; set; }
    public ObservableCollection<FolderNodeViewModel> Children { get; } = new();

    public string FullPath {
        get {
            var pathParts = new List<string> { Name };
            var current = ParentNode;
            while (current != null) {
                pathParts.Add(current.Name);
                current = current.ParentNode;
            }
            pathParts.Reverse();
            return "Library / " + string.Join(" / ", pathParts);
        }
    }

    partial void OnIsSelectedChanged(bool value) {
        if (value && _onSelectionChanged != null) {
            _onSelectionChanged(this);
        }
    }

    public static ObservableCollection<FolderNodeViewModel> BuildFolderTree(
        List<Folder> folders,
        Action<FolderNodeViewModel> onSelectionChanged) {

        var roots = new ObservableCollection<FolderNodeViewModel>();

        // 1. Build folder map
        var folderMap = folders.ToDictionary(f => f.Id);

        // 2. Create view models for all folders
        var vmMap = folders.ToDictionary(
            f => f.Id,
            f => new FolderNodeViewModel(f, f.Name, onSelectionChanged)
        );

        // 3. Link children and parents
        foreach (var f in folders) {
            var vm = vmMap[f.Id];
            if (f.ParentId.HasValue && vmMap.TryGetValue(f.ParentId.Value, out var parentVm)) {
                parentVm.Children.Add(vm);
                vm.ParentNode = parentVm;
            } else {
                roots.Add(vm);
            }
        }

        // Sort children
        foreach (var vm in vmMap.Values) {
            vm.SortChildren();
        }

        // Sort roots alphabetically
        var sortedRoots = roots.OrderBy(c => c.Name).ToList();
        roots.Clear();
        foreach (var r in sortedRoots) {
            roots.Add(r);
        }

        return roots;
    }

    private void SortChildren() {
        if (Children.Count <= 1) return;
        var sorted = Children.OrderBy(c => c.Name).ToList();
        Children.Clear();
        foreach (var child in sorted) {
            Children.Add(child);
        }
    }
}
