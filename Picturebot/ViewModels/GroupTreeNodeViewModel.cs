using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Models;

namespace Picturebot.ViewModels;

public partial class GroupTreeNodeViewModel : ViewModelBase {
    private readonly Action<GroupTreeNodeViewModel>? _onCommit;
    private readonly Action<GroupTreeNodeViewModel>? _onCancel;
    private readonly Action<GroupTreeNodeViewModel>? _onDelete;
    private readonly Action<GroupTreeNodeViewModel>? _onAddChildTag;

    public bool IsGroup { get; }
    public bool IsTag => !IsGroup;

    public TagGroup? GroupModel { get; set; }
    public Tag? TagModel { get; set; }

    public GroupTreeNodeViewModel? ParentGroup { get; set; }
    public ObservableCollection<GroupTreeNodeViewModel> Children { get; } = new();

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

    // Constructor for Group (Parent)
    public GroupTreeNodeViewModel(
        TagGroup groupModel,
        Action<GroupTreeNodeViewModel>? onCommit = null,
        Action<GroupTreeNodeViewModel>? onCancel = null,
        Action<GroupTreeNodeViewModel>? onDelete = null,
        Action<GroupTreeNodeViewModel>? onAddChildTag = null) {
        IsGroup = true;
        GroupModel = groupModel;
        _name = groupModel.GroupName;
        _editingName = groupModel.GroupName;
        _onCommit = onCommit;
        _onCancel = onCancel;
        _onDelete = onDelete;
        _onAddChildTag = onAddChildTag;
    }

    // Constructor for Tag (Child)
    public GroupTreeNodeViewModel(
        Tag tagModel,
        GroupTreeNodeViewModel parentGroup,
        Action<GroupTreeNodeViewModel>? onCommit = null,
        Action<GroupTreeNodeViewModel>? onCancel = null,
        Action<GroupTreeNodeViewModel>? onDelete = null) {
        IsGroup = false;
        TagModel = tagModel;
        ParentGroup = parentGroup;
        _name = tagModel.Name;
        _tagId = tagModel.Id;
        _editingName = tagModel.Name;
        _onCommit = onCommit;
        _onCancel = onCancel;
        _onDelete = onDelete;
    }

    // Constructor for new uncommitted item (group or tag)
    public GroupTreeNodeViewModel(
        bool isGroup,
        string name,
        GroupTreeNodeViewModel? parentGroup = null,
        Action<GroupTreeNodeViewModel>? onCommit = null,
        Action<GroupTreeNodeViewModel>? onCancel = null,
        Action<GroupTreeNodeViewModel>? onDelete = null,
        Action<GroupTreeNodeViewModel>? onAddChildTag = null) {
        IsGroup = isGroup;
        ParentGroup = parentGroup;
        _name = name;
        _editingName = name;
        _onCommit = onCommit;
        _onCancel = onCancel;
        _onDelete = onDelete;
        _onAddChildTag = onAddChildTag;
        if (isGroup) {
            GroupModel = new TagGroup { GroupName = name };
        }
    }

    partial void OnNameChanged(string value) {
        if (IsGroup && GroupModel != null) {
            GroupModel.GroupName = value;
        } else if (IsTag && TagModel != null) {
            TagModel.Name = value;
        }
    }

    partial void OnTagIdChanged(Guid? value) {
        if (IsTag && TagModel != null && value.HasValue) {
            TagModel.Id = value.Value;
        }
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

    [RelayCommand]
    public void Delete() {
        _onDelete?.Invoke(this);
    }

    [RelayCommand]
    public void AddChildTag() {
        _onAddChildTag?.Invoke(this);
    }

    public string GetBreadcrumbPath() {
        if (IsGroup) return Name;
        return ParentGroup != null && !string.IsNullOrWhiteSpace(ParentGroup.Name)
            ? $"{ParentGroup.Name} > {Name}"
            : Name;
    }
}
