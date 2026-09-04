using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Picturebot.ViewModels;

public partial class TagFilterNodeViewModel : ViewModelBase {
    private readonly Action? _onToggled;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool? _isChecked = false;

    [ObservableProperty]
    private bool _isExpanded = true;

    private bool _isUpdatingState;

    [ObservableProperty]
    private bool _isVisible = true;

    public TagFilterNodeViewModel(
        string name,
        string fullPath,
        int count = 0,
        bool? isChecked = false,
        TagFilterNodeViewModel? parent = null,
        Action? onToggled = null) {
        Name = name;
        FullPath = fullPath;
        _count = count;
        _isChecked = isChecked;
        Parent = parent;
        _onToggled = onToggled;
    }

    public string Name { get; }
    public string FullPath { get; }
    public TagFilterNodeViewModel? Parent { get; }
    public ObservableCollection<TagFilterNodeViewModel> Children { get; } = new();
    public ObservableCollection<TagFilterNodeViewModel> VisibleChildren { get; } = new();

    public bool HasChildren => Children.Count > 0;

    partial void OnIsCheckedChanged(bool? value) {
        if (_isUpdatingState) {
            return;
        }

        // Propagate state down to descendants
        if (value.HasValue) {
            SetChildrenChecked(value.Value);
        } else {
            // If transitioned to null directly, toggle down to false
            SetChildrenChecked(false);
        }

        // Propagate state up to ancestors
        Parent?.UpdateParentState();

        _onToggled?.Invoke();
    }

    public void SetCheckedRecursive(bool isChecked) {
        _isUpdatingState = true;
        try {
            IsChecked = isChecked;
            foreach (var child in Children) {
                child.SetCheckedRecursive(isChecked);
            }
        } finally {
            _isUpdatingState = false;
        }
    }

    private void SetChildrenChecked(bool isChecked) {
        _isUpdatingState = true;
        try {
            foreach (var child in Children) {
                child.IsChecked = isChecked;
                child.SetChildrenChecked(isChecked);
            }
        } finally {
            _isUpdatingState = false;
        }
    }

    public void UpdateParentState() {
        if (_isUpdatingState) {
            return;
        }

        if (Children.Count == 0) {
            return;
        }

        var allChecked = true;
        var allUnchecked = true;

        foreach (var child in Children) {
            if (child.IsChecked == true) {
                allUnchecked = false;
            } else if (child.IsChecked == false) {
                allChecked = false;
            } else // child.IsChecked is null (indeterminate)
            {
                allChecked = false;
                allUnchecked = false;
            }
        }

        bool? newState = allChecked ? true : allUnchecked ? false : null;

        if (IsChecked != newState) {
            _isUpdatingState = true;
            try {
                IsChecked = newState;
            } finally {
                _isUpdatingState = false;
            }

            Parent?.UpdateParentState();
        }
    }

    public void RecalculateStateFromChildren() {
        if (Children.Count == 0) {
            return;
        }

        foreach (var child in Children) {
            child.RecalculateStateFromChildren();
        }

        var allChecked = true;
        var allUnchecked = true;

        foreach (var child in Children) {
            if (child.IsChecked == true) {
                allUnchecked = false;
            } else if (child.IsChecked == false) {
                allChecked = false;
            } else {
                allChecked = false;
                allUnchecked = false;
            }
        }

        _isUpdatingState = true;
        try {
            IsChecked = allChecked ? true : allUnchecked ? false : null;
        } finally {
            _isUpdatingState = false;
        }
    }

    public bool FilterSearch(string? searchText) {
        if (string.IsNullOrWhiteSpace(searchText)) {
            IsVisible = true;
            VisibleChildren.Clear();
            foreach (var child in Children) {
                child.FilterSearch(null);
                VisibleChildren.Add(child);
            }

            return true;
        }

        var selfMatches = Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          FullPath.Contains(searchText, StringComparison.OrdinalIgnoreCase);

        VisibleChildren.Clear();
        var anyChildMatches = false;

        foreach (var child in Children) {
            if (child.FilterSearch(searchText)) {
                VisibleChildren.Add(child);
                anyChildMatches = true;
            }
        }

        IsVisible = selfMatches || anyChildMatches;

        if (selfMatches && !anyChildMatches) {
            // If parent matches, show all its children
            foreach (var child in Children) {
                child.FilterSearch(null);
                VisibleChildren.Add(child);
            }
        }

        if (IsVisible && anyChildMatches) {
            IsExpanded = true;
        }

        return IsVisible;
    }

    public void CollectSelectedPaths(HashSet<string> selectedPaths) {
        if (Children.Count == 0) {
            if (IsChecked == true) {
                selectedPaths.Add(FullPath);
                selectedPaths.Add(Name);
            }
        } else {
            if (IsChecked == true) {
                selectedPaths.Add(FullPath);
                selectedPaths.Add(Name);
            }

            foreach (var child in Children) {
                child.CollectSelectedPaths(selectedPaths);
            }
        }
    }

    public IEnumerable<TagFilterNodeViewModel> GetAllNodes() {
        yield return this;
        foreach (var child in Children) {
            foreach (var descendant in child.GetAllNodes()) {
                yield return descendant;
            }
        }
    }

    public IEnumerable<TagFilterNodeViewModel> GetAllLeaves() {
        if (Children.Count == 0) {
            yield return this;
        } else {
            foreach (var child in Children) {
                foreach (var leaf in child.GetAllLeaves()) {
                    yield return leaf;
                }
            }
        }
    }
}
