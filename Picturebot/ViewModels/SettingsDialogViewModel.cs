using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;
using Picturebot.Views;
using Serilog;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public class TaxonomyBranchItem {
    public HierarchyNodeViewModel Node { get; set; } = null!;
    public string DisplayPath { get; set; } = string.Empty;
    public override string ToString() => DisplayPath;
}

public partial class TagGroupItemViewModel : ViewModelBase {
    public TagGroup Model { get; }
    private readonly Action<TagGroupItemViewModel> _deleteAction;
    private readonly Action<TagGroupItemViewModel, string> _renameAction;
    private readonly Action<TagGroupItemViewModel>? _cancelAction;

    public Guid GroupId => Model.GroupId;
    public ObservableCollection<Guid> TagIds => Model.TagIds;

    [ObservableProperty]
    private string _groupName;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editingName = string.Empty;

    public bool IsNewUncommitted { get; set; }
    public bool IsNewNode { get; set; }

    public int TagCount => TagIds.Count;
    public string TagCountBadge => TagIds.Count == 1 ? "1 tag" : $"{TagIds.Count} tags";

    public TagGroupItemViewModel(
        TagGroup model, 
        Action<TagGroupItemViewModel> deleteAction, 
        Action<TagGroupItemViewModel, string> renameAction,
        Action<TagGroupItemViewModel>? cancelAction = null) {
        Model = model;
        _deleteAction = deleteAction;
        _renameAction = renameAction;
        _cancelAction = cancelAction;
        _groupName = model.GroupName;
        _editingName = model.GroupName;
    }

    public void NotifyTagCountChanged() {
        OnPropertyChanged(nameof(TagCount));
        OnPropertyChanged(nameof(TagCountBadge));
    }

    [RelayCommand]
    public void StartEdit() {
        EditingName = GroupName;
        IsEditing = true;
    }

    [RelayCommand]
    public void CommitEdit() {
        var trimmed = EditingName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed)) {
            if (IsNewUncommitted || IsNewNode) {
                _cancelAction?.Invoke(this);
            } else {
                EditingName = GroupName;
                IsEditing = false;
            }
            return;
        }

        IsNewUncommitted = false;
        IsNewNode = false;
        _renameAction(this, trimmed);
        IsEditing = false;
    }

    [RelayCommand]
    public void CancelEdit() {
        if (IsNewUncommitted || IsNewNode) {
            _cancelAction?.Invoke(this);
        } else {
            EditingName = GroupName;
            IsEditing = false;
        }
    }

    [RelayCommand]
    public void Delete() {
        _deleteAction(this);
    }
}

public partial class SettingsDialogViewModel : ViewModelBase {
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _blueLabelName = "Blue";

    [ObservableProperty]
    private string _blueLabelShortcut = "Ctrl+NumPad5";

    [ObservableProperty]
    private int _burstFallbackThreshold = 10;

    [ObservableProperty]
    private int _burstTimeThreshold = 3;

    [ObservableProperty]
    private string _editFolderPath = string.Empty;

    [ObservableProperty]
    private string _fullscreenShortcut = "F";

    [ObservableProperty]
    private string _greenLabelName = "Green";

    [ObservableProperty]
    private string _greenLabelShortcut = "Ctrl+NumPad4";

    [ObservableProperty]
    private int _groupingThreshold = 10;

    [ObservableProperty]
    private bool _launchFullScreen;

    [ObservableProperty]
    private string _libraryLocation = string.Empty;

    [ObservableProperty]
    private string _noneLabelShortcut = "Ctrl+NumPad0";

    [ObservableProperty]
    private string _openInExplorerShortcut = "O";

    [ObservableProperty]
    private string _orangeLabelName = "Orange";

    [ObservableProperty]
    private string _orangeLabelShortcut = "Ctrl+NumPad2";

    [ObservableProperty]
    private string _pinkLabelName = "Pink";

    [ObservableProperty]
    private string _pinkLabelShortcut = "Ctrl+NumPad6";

    [ObservableProperty]
    private string _printFolderPath = string.Empty;

    [ObservableProperty]
    private string _purpleLabelName = "Purple";

    [ObservableProperty]
    private string _purpleLabelShortcut = "Ctrl+NumPad7";

    [ObservableProperty]
    private string _rating0Shortcut = "NumPad0";

    [ObservableProperty]
    private string _rating1Shortcut = "NumPad1";

    [ObservableProperty]
    private string _rating2Shortcut = "NumPad2";

    [ObservableProperty]
    private string _rating3Shortcut = "NumPad3";

    [ObservableProperty]
    private string _rating4Shortcut = "NumPad4";

    [ObservableProperty]
    private string _rating5Shortcut = "NumPad5";

    [ObservableProperty]
    private string _curationPickedShortcut = "P";

    [ObservableProperty]
    private string _curationRejectedShortcut = "X";

    [ObservableProperty]
    private string _curationNeutralShortcut = "U";

    [ObservableProperty]
    private string _copyToEditShortcut = "Ctrl+E";

    [ObservableProperty]
    private string _copyToPrintShortcut = "Shift+E";

    [ObservableProperty]
    private string _redLabelName = "Red";

    [ObservableProperty]
    private string _redLabelShortcut = "Ctrl+NumPad1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightActive))]
    [NotifyPropertyChangedFor(nameof(IsDarkActive))]
    [NotifyPropertyChangedFor(nameof(IsSystemActive))]
    private int _themeIndex;

    [ObservableProperty]
    private string _yellowLabelName = "Yellow";

    [ObservableProperty]
    private string _yellowLabelShortcut = "Ctrl+NumPad3";

    // Active Navigation Page (0=General, 1=Storage, 2=Culling, 3=ColorLabels, 4=Shortcuts, 5=TagsCatalog, 6=Taxonomy, 7=Groups)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTabActive))]
    [NotifyPropertyChangedFor(nameof(IsStorageTabActive))]
    [NotifyPropertyChangedFor(nameof(IsCullingTabActive))]
    [NotifyPropertyChangedFor(nameof(IsColorLabelsTabActive))]
    [NotifyPropertyChangedFor(nameof(IsShortcutsTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTagsCatalogTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTaxonomyTabActive))]
    [NotifyPropertyChangedFor(nameof(IsGroupsTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTagsCategoryActive))]
    [NotifyPropertyChangedFor(nameof(IsTagsSubTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTaxonomySubTabActive))]
    [NotifyPropertyChangedFor(nameof(IsGroupsSubTabActive))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTaxonomyNode))]
    [NotifyPropertyChangedFor(nameof(IsTaxonomyPathVisible))]
    [NotifyPropertyChangedFor(nameof(HasSelectedGroupNode))]
    [NotifyPropertyChangedFor(nameof(SelectedGroupPath))]
    [NotifyPropertyChangedFor(nameof(HasSelectedBreadcrumb))]
    [NotifyPropertyChangedFor(nameof(SelectedBreadcrumbLabel))]
    [NotifyPropertyChangedFor(nameof(SelectedBreadcrumbPath))]
    [NotifyPropertyChangedFor(nameof(SelectedTabIndex))]
    [NotifyPropertyChangedFor(nameof(KeywordsSubTabIndex))]
    private int _activeViewIndex;

    // Sidebar Expandable Menu State (Default: Collapsed)
    [ObservableProperty]
    private bool _isTagsMenuExpanded;

    public bool IsGeneralTabActive => ActiveViewIndex == 0;
    public bool IsStorageTabActive => ActiveViewIndex == 1;
    public bool IsCullingTabActive => ActiveViewIndex == 2;
    public bool IsColorLabelsTabActive => ActiveViewIndex == 3;
    public bool IsShortcutsTabActive => ActiveViewIndex == 4;
    public bool IsTagsCatalogTabActive => ActiveViewIndex == 5;
    public bool IsTaxonomyTabActive => ActiveViewIndex == 6;
    public bool IsGroupsTabActive => ActiveViewIndex == 7;
    public bool IsTagsCategoryActive => ActiveViewIndex >= 5 && ActiveViewIndex <= 7;

    public bool IsTagsSubTabActive => IsTagsCatalogTabActive;
    public bool IsTaxonomySubTabActive => IsTaxonomyTabActive;
    public bool IsGroupsSubTabActive => IsGroupsTabActive;

    public bool HasSelectedTaxonomyNode =>
        IsTaxonomyTabActive && SelectedHierarchyNode != null && !string.IsNullOrWhiteSpace(SelectedHierarchyNode.Name);

    public bool IsTaxonomyPathVisible => HasSelectedTaxonomyNode;

    public string SelectedTaxonomyPath => CalculatedBreadcrumbPath;

    public bool HasSelectedGroupNode =>
        IsGroupsTabActive && SelectedGroupTreeNode != null && !string.IsNullOrWhiteSpace(SelectedGroupTreeNode.Name);

    public string SelectedGroupPath =>
        SelectedGroupTreeNode?.GetBreadcrumbPath() ?? string.Empty;

    public bool HasSelectedBreadcrumb =>
        HasSelectedTaxonomyNode || HasSelectedGroupNode;

    public string SelectedBreadcrumbLabel => "Path:";

    public string SelectedBreadcrumbPath =>
        IsTaxonomyTabActive ? SelectedTaxonomyPath : (IsGroupsTabActive ? SelectedGroupPath : string.Empty);

    public int SelectedTabIndex {
        get => ActiveViewIndex switch {
            <= 4 => ActiveViewIndex,
            _ => 5
        };
        set {
            if (value <= 4) {
                ActiveViewIndex = value;
                IsTagsMenuExpanded = false;
            } else if (value == 5) {
                ActiveViewIndex = 5 + KeywordsSubTabIndex;
                IsTagsMenuExpanded = true;
            }
        }
    }

    public int KeywordsSubTabIndex {
        get => ActiveViewIndex switch {
            5 => 0,
            6 => 1,
            7 => 2,
            _ => 0
        };
        set {
            if (value is >= 0 and <= 2) {
                ActiveViewIndex = 5 + value;
                IsTagsMenuExpanded = true;
            }
        }
    }

    public SettingsDialogViewModel(ISettingsService settingsService) {
        _settingsService = settingsService;
        LoadSettings();
    }

    public SettingsDialogViewModel() {
        _settingsService = null!;
    }

    public bool IsLightActive => ThemeIndex == 0;
    public bool IsDarkActive => ThemeIndex == 1;
    public bool IsSystemActive => ThemeIndex == 2;

    private void LoadSettings() {
        if (_settingsService == null) {
            return;
        }

        LoadSettingsFromModel(_settingsService.Current);
    }

    private void LoadSettingsFromModel(SettingsModel settings) {
        LibraryLocation = settings.LibraryPath ?? string.Empty;
        GroupingThreshold = settings.GroupingThreshold;
        BurstTimeThreshold = settings.BurstTimeThresholdSeconds;
        BurstFallbackThreshold = settings.BurstFallbackTimeThresholdSeconds;
        LaunchFullScreen = settings.LaunchMaximized;
        RedLabelName = settings.RedLabelName;
        OrangeLabelName = settings.OrangeLabelName;
        YellowLabelName = settings.YellowLabelName;
        GreenLabelName = settings.GreenLabelName;
        BlueLabelName = settings.BlueLabelName;
        PinkLabelName = settings.PinkLabelName;
        PurpleLabelName = settings.PurpleLabelName;
        RedLabelShortcut = !string.IsNullOrWhiteSpace(settings.RedLabelShortcut) ? settings.RedLabelShortcut : "Ctrl+NumPad1";
        OrangeLabelShortcut = !string.IsNullOrWhiteSpace(settings.OrangeLabelShortcut) ? settings.OrangeLabelShortcut : "Ctrl+NumPad2";
        YellowLabelShortcut = !string.IsNullOrWhiteSpace(settings.YellowLabelShortcut) ? settings.YellowLabelShortcut : "Ctrl+NumPad3";
        GreenLabelShortcut = !string.IsNullOrWhiteSpace(settings.GreenLabelShortcut) ? settings.GreenLabelShortcut : "Ctrl+NumPad4";
        BlueLabelShortcut = !string.IsNullOrWhiteSpace(settings.BlueLabelShortcut) ? settings.BlueLabelShortcut : "Ctrl+NumPad5";
        PinkLabelShortcut = !string.IsNullOrWhiteSpace(settings.PinkLabelShortcut) ? settings.PinkLabelShortcut : "Ctrl+NumPad6";
        PurpleLabelShortcut = !string.IsNullOrWhiteSpace(settings.PurpleLabelShortcut) ? settings.PurpleLabelShortcut : "Ctrl+NumPad7";
        NoneLabelShortcut = !string.IsNullOrWhiteSpace(settings.NoneLabelShortcut) ? settings.NoneLabelShortcut : "Ctrl+NumPad0";
        FullscreenShortcut = !string.IsNullOrWhiteSpace(settings.FullscreenShortcut) ? settings.FullscreenShortcut : "F";
        OpenInExplorerShortcut = !string.IsNullOrWhiteSpace(settings.OpenInExplorerShortcut) ? settings.OpenInExplorerShortcut : "O";
        Rating0Shortcut = !string.IsNullOrWhiteSpace(settings.Rating0Shortcut) ? settings.Rating0Shortcut : "NumPad0";
        Rating1Shortcut = !string.IsNullOrWhiteSpace(settings.Rating1Shortcut) ? settings.Rating1Shortcut : "NumPad1";
        Rating2Shortcut = !string.IsNullOrWhiteSpace(settings.Rating2Shortcut) ? settings.Rating2Shortcut : "NumPad2";
        Rating3Shortcut = !string.IsNullOrWhiteSpace(settings.Rating3Shortcut) ? settings.Rating3Shortcut : "NumPad3";
        Rating4Shortcut = !string.IsNullOrWhiteSpace(settings.Rating4Shortcut) ? settings.Rating4Shortcut : "NumPad4";
        Rating5Shortcut = !string.IsNullOrWhiteSpace(settings.Rating5Shortcut) ? settings.Rating5Shortcut : "NumPad5";
        CurationPickedShortcut = !string.IsNullOrWhiteSpace(settings.CurationPickedShortcut) ? settings.CurationPickedShortcut : "P";
        CurationRejectedShortcut = !string.IsNullOrWhiteSpace(settings.CurationRejectedShortcut) ? settings.CurationRejectedShortcut : "X";
        CurationNeutralShortcut = !string.IsNullOrWhiteSpace(settings.CurationNeutralShortcut) ? settings.CurationNeutralShortcut : "U";
        CopyToEditShortcut = !string.IsNullOrWhiteSpace(settings.CopyToEditShortcut) ? settings.CopyToEditShortcut : "Ctrl+E";
        CopyToPrintShortcut = !string.IsNullOrWhiteSpace(settings.CopyToPrintShortcut) ? settings.CopyToPrintShortcut : "Shift+E";
        EditFolderPath = settings.EditFolderPath ?? string.Empty;
        PrintFolderPath = settings.PrintFolderPath ?? string.Empty;

        // Tags (Catalog) - Alphabetical order & lowercase normalization
        Tags.Clear();
        MasterTags.Clear();
        var sortedTags = settings.MasterTags
            .Select(t => {
                t.Name = t.Name.Trim().ToLowerInvariant();
                return t;
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var tag in sortedTags) {
            MasterTags.Add(tag);
            Tags.Add(new TagItemViewModel(tag, RequestDeleteTag, OnTagRenamed));
        }

        // Taxonomy Nodes
        HierarchyNodes.Clear();
        foreach (var node in settings.HierarchyNodes) {
            NormalizeNodeNames(node);
            HierarchyNodes.Add(new HierarchyNodeViewModel(node, null, OnNodeCommit, OnNodeCancel));
        }
        SelectedHierarchyNode = null;

        // Groups (Single-Container 2-Level Expandable Tree & Legacy List)
        TagGroupTreeNodes.Clear();
        TagGroups.Clear();
        foreach (var group in settings.TagGroups) {
            var groupNode = new GroupTreeNodeViewModel(group, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete, OnGroupTreeAddChildTag);
            foreach (var tagId in group.TagIds) {
                var tag = MasterTags.FirstOrDefault(t => t.Id == tagId) ?? Tags.FirstOrDefault(t => t.Id == tagId)?.Model;
                if (tag != null) {
                    groupNode.Children.Add(new GroupTreeNodeViewModel(tag, groupNode, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete));
                }
            }
            TagGroupTreeNodes.Add(groupNode);
            TagGroups.Add(new TagGroupItemViewModel(group, DeleteTagGroupItem, RenameTagGroupItem, CancelTagGroupItem));
        }

        SelectedGroupTreeNode = TagGroupTreeNodes.FirstOrDefault(g => g.GroupModel?.GroupId == settings.ActiveTagGroupId) ?? TagGroupTreeNodes.FirstOrDefault();
        SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupId == settings.ActiveTagGroupId) ?? TagGroups.FirstOrDefault();
        RefreshTagSuggestions();
        RefreshAvailableTaxonomyBranches();
        OnPropertyChanged(nameof(GroupsHeaderTitle));

        ThemeIndex = settings.ThemeMode switch {
            ThemeMode.Light => 0,
            ThemeMode.Dark => 1,
            _ => 2
        };
    }

    private static void NormalizeNodeNames(HierarchyNode node) {
        node.Name = node.Name.Trim().ToLowerInvariant();
        foreach (var child in node.Children) {
            NormalizeNodeNames(child);
        }
    }

    [RelayCommand]
    private void SetLightTheme() => ThemeIndex = 0;

    [RelayCommand]
    private void SetDarkTheme() => ThemeIndex = 1;

    [RelayCommand]
    private void SetSystemTheme() => ThemeIndex = 2;

    [RelayCommand]
    public void SelectGeneralTab() {
        IsTagsMenuExpanded = false;
        ActiveViewIndex = 0;
    }

    [RelayCommand]
    public void SelectStorageTab() {
        IsTagsMenuExpanded = false;
        ActiveViewIndex = 1;
    }

    [RelayCommand]
    public void SelectCullingTab() {
        IsTagsMenuExpanded = false;
        ActiveViewIndex = 2;
    }

    [RelayCommand]
    public void SelectColorLabelsTab() {
        IsTagsMenuExpanded = false;
        ActiveViewIndex = 3;
    }

    [RelayCommand]
    public void SelectShortcutsTab() {
        IsTagsMenuExpanded = false;
        ActiveViewIndex = 4;
    }

    [RelayCommand]
    public void SelectTagsCategory() {
        IsTagsMenuExpanded = true;
        if (ActiveViewIndex < 5 || ActiveViewIndex > 7) {
            ActiveViewIndex = 5;
        }
    }

    [RelayCommand]
    public void ToggleTagsCategory() {
        SelectTagsCategory();
    }

    [RelayCommand]
    public void SelectTagsCatalogTab() {
        IsTagsMenuExpanded = true;
        ActiveViewIndex = 5;
    }

    [RelayCommand]
    public void SelectTaxonomyTab() {
        IsTagsMenuExpanded = true;
        ActiveViewIndex = 6;
    }

    [RelayCommand]
    public void SelectGroupsTab() {
        IsTagsMenuExpanded = true;
        ActiveViewIndex = 7;
    }

    [RelayCommand]
    public void SelectTagsSubTab() => SelectTagsCatalogTab();

    [RelayCommand]
    public void SelectTaxonomySubTab() => SelectTaxonomyTab();

    [RelayCommand]
    public void SelectGroupsSubTab() => SelectGroupsTab();

    [RelayCommand]
    public void SelectKeywordsSubTab(string? indexStr) {
        if (int.TryParse(indexStr, out var idx)) {
            KeywordsSubTabIndex = idx;
        }
    }

    [RelayCommand]
    private async Task BrowseLibraryLocation() {
        LibraryLocation = await BrowseFolder(LibraryLocation, "Select Library Location") ?? LibraryLocation;
    }

    [RelayCommand]
    private async Task BrowseEditFolder() {
        EditFolderPath = await BrowseFolder(EditFolderPath, "Select Edit Destination Folder") ?? EditFolderPath;
    }

    [RelayCommand]
    private async Task BrowsePrintFolder() {
        PrintFolderPath = await BrowseFolder(PrintFolderPath, "Select Print Destination Folder") ?? PrintFolderPath;
    }

    private async Task<string?> BrowseFolder(string currentPath, string title) {
        try {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow is not Window window) {
                return null;
            }

            var options = new FolderPickerOpenOptions {
                Title = title,
                AllowMultiple = false
            };

            var startPath = currentPath;
            if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath)) {
                startPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (!Directory.Exists(startPath)) {
                    startPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
            }

            if (Directory.Exists(startPath)) {
                try {
                    options.SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(startPath);
                } catch (Exception ex) {
                    Log.Debug(ex, "Could not set suggested start location for folder picker");
                }
            }

            var folders = await window.StorageProvider.OpenFolderPickerAsync(options);

            if (folders != null && folders.Count > 0) {
                return folders[0].Path.LocalPath;
            }
        } catch (Exception ex) {
            Log.Error(ex, "Error browsing folder for {Title}", title);
        }

        return null;
    }

    [RelayCommand]
    private async Task SaveSettings(object? parameter) {
        if (_settingsService == null) {
            return;
        }

        await _settingsService.UpdateAsync(BuildCurrentSettingsModel());

        try {
            if (Application.Current != null) {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Settings")
                    .WithContent("Your preferences have been updated successfully.")
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        } catch {
            // Ignored in test/headless environments
        }

        CloseDialog(parameter);
    }

    [RelayCommand]
    private void DiscardChanges() {
        LoadSettings();
        try {
            if (Application.Current != null) {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Settings")
                    .WithContent("Unsaved changes have been discarded.")
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(2))
                    .Queue();
            }
        } catch {
            // Ignored in test/headless environments
        }
    }

    private SettingsModel BuildCurrentSettingsModel() {
        return new SettingsModel {
            LibraryPath = LibraryLocation,
            GroupingThreshold = GroupingThreshold,
            BurstTimeThresholdSeconds = BurstTimeThreshold,
            BurstFallbackTimeThresholdSeconds = BurstFallbackThreshold,
            LaunchMaximized = LaunchFullScreen,
            RedLabelName = RedLabelName,
            OrangeLabelName = OrangeLabelName,
            YellowLabelName = YellowLabelName,
            GreenLabelName = GreenLabelName,
            BlueLabelName = BlueLabelName,
            PinkLabelName = PinkLabelName,
            PurpleLabelName = PurpleLabelName,
            RedLabelShortcut = RedLabelShortcut,
            OrangeLabelShortcut = OrangeLabelShortcut,
            YellowLabelShortcut = YellowLabelShortcut,
            GreenLabelShortcut = GreenLabelShortcut,
            BlueLabelShortcut = BlueLabelShortcut,
            PinkLabelShortcut = PinkLabelShortcut,
            PurpleLabelShortcut = PurpleLabelShortcut,
            NoneLabelShortcut = NoneLabelShortcut,
            FullscreenShortcut = FullscreenShortcut,
            OpenInExplorerShortcut = OpenInExplorerShortcut,
            Rating0Shortcut = Rating0Shortcut,
            Rating1Shortcut = Rating1Shortcut,
            Rating2Shortcut = Rating2Shortcut,
            Rating3Shortcut = Rating3Shortcut,
            Rating4Shortcut = Rating4Shortcut,
            Rating5Shortcut = Rating5Shortcut,
            CurationPickedShortcut = CurationPickedShortcut,
            CurationRejectedShortcut = CurationRejectedShortcut,
            CurationNeutralShortcut = CurationNeutralShortcut,
            CopyToEditShortcut = CopyToEditShortcut,
            CopyToPrintShortcut = CopyToPrintShortcut,
            EditFolderPath = EditFolderPath,
            PrintFolderPath = PrintFolderPath,
            MasterTags = Tags.Select(t => t.Model).ToList(),
            HierarchyNodes = HierarchyNodes.Select(n => n.ToModel()).ToList(),
            TagGroups = TagGroupTreeNodes.Select(g => new TagGroup {
                GroupId = g.GroupModel?.GroupId ?? Guid.NewGuid(),
                GroupName = g.Name,
                TagIds = new ObservableCollection<Guid>(g.Children.Where(c => c.TagId.HasValue).Select(c => c.TagId!.Value))
            }).ToList(),
            ActiveTagGroupId = (SelectedGroupTreeNode?.IsGroup == true ? SelectedGroupTreeNode.GroupModel?.GroupId : SelectedGroupTreeNode?.ParentGroup?.GroupModel?.GroupId) ?? TagGroupTreeNodes.FirstOrDefault()?.GroupModel?.GroupId,
            ThemeMode = ThemeIndex switch {
                0 => ThemeMode.Light,
                1 => ThemeMode.Dark,
                _ => ThemeMode.System
            }
        };
    }

    [RelayCommand]
    private void CloseDialog(object? parameter) {
        if (parameter is Window window) {
            window.Close();
        } else {
            try {
                MainWindow.DialogManager.DismissDialog();
            } catch {
                // Ignored
            }
        }
    }

    // ── SUB-TAB 1: TAGS (CATALOG) ──────────────────────────────────────────
    public ObservableCollection<TagItemViewModel> Tags { get; } = new();
    public ObservableCollection<Tag> MasterTags { get; } = new();

    [ObservableProperty]
    private string _newTagName = string.Empty;

    // Delete confirmation state
    [ObservableProperty]
    private bool _isDeleteTagConfirmOpen;

    [ObservableProperty]
    private string _deleteTagConfirmTitle = "Delete Tag";

    [ObservableProperty]
    private string _deleteTagConfirmMessage = string.Empty;

    private TagItemViewModel? _tagPendingDelete;

    private void InsertTagSorted(TagItemViewModel tagItem) {
        int index = 0;
        while (index < Tags.Count && string.Compare(Tags[index].Name, tagItem.Name, StringComparison.Ordinal) < 0) {
            index++;
        }
        Tags.Insert(index, tagItem);
        MasterTags.Insert(index, tagItem.Model);
    }

    [RelayCommand]
    private void AddTag() {
        var name = NewTagName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!Tags.Any(t => t.Name.Equals(name, StringComparison.Ordinal))) {
            var newTag = new Tag { Name = name };
            var tagItem = new TagItemViewModel(newTag, RequestDeleteTag, OnTagRenamed);
            InsertTagSorted(tagItem);

            RefreshTagGroupViews();
            RefreshTagSuggestions();
        }
        NewTagName = string.Empty;
    }

    public void RequestDeleteTag(TagItemViewModel tagItem) {
        // Check if tag is used in taxonomy or groups
        var taxCount = CountTagUsageInTree(HierarchyNodes, tagItem.Id, tagItem.Name);
        var groupCount = TagGroups.Count(g => g.TagIds.Contains(tagItem.Id));

        if (taxCount > 0 || groupCount > 0) {
            _tagPendingDelete = tagItem;
            DeleteTagConfirmTitle = $"Delete '{tagItem.Name}'";
            var usageList = new List<string>();
            if (taxCount > 0) usageList.Add($"{taxCount} taxonomy node(s)");
            if (groupCount > 0) usageList.Add($"{groupCount} group(s)");

            DeleteTagConfirmMessage = $"This tag is currently used in {string.Join(" and ", usageList)}. Deleting it will unlink it from taxonomy and remove it from groups. Are you sure you want to proceed?";
            IsDeleteTagConfirmOpen = true;
        } else {
            ExecuteDeleteTag(tagItem);
        }
    }

    [RelayCommand]
    private void ConfirmDeleteTag() {
        if (_tagPendingDelete != null) {
            ExecuteDeleteTag(_tagPendingDelete);
            _tagPendingDelete = null;
        }
        IsDeleteTagConfirmOpen = false;
    }

    [RelayCommand]
    private void CancelDeleteTag() {
        _tagPendingDelete = null;
        IsDeleteTagConfirmOpen = false;
    }

    private void ExecuteDeleteTag(TagItemViewModel tagItem) {
        var tagId = tagItem.Id;

        // 1. Remove from collections
        Tags.Remove(tagItem);
        var masterMatch = MasterTags.FirstOrDefault(t => t.Id == tagId);
        if (masterMatch != null) {
            MasterTags.Remove(masterMatch);
        }

        // 2. Unlink from HierarchyNodes
        UnlinkTagFromTree(HierarchyNodes, tagId);

        // 3. Remove from all TagGroups
        foreach (var group in TagGroups) {
            group.TagIds.Remove(tagId);
            group.NotifyTagCountChanged();
        }

        RefreshTagGroupViews();
        RefreshTagSuggestions();
        RefreshAvailableTaxonomyBranches();
        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
        OnPropertyChanged(nameof(SelectedNodeFullPath));
        OnPropertyChanged(nameof(SelectedTaxonomyPath));
    }

    private void OnTagRenamed(TagItemViewModel tagItem) {
        var newName = tagItem.Name.Trim().ToLowerInvariant();

        // Check if another tag already has this name
        var duplicate = Tags.FirstOrDefault(t => t != tagItem && t.Name.Equals(newName, StringComparison.Ordinal));
        if (duplicate != null) {
            // Revert rename to prevent duplicate tags
            tagItem.Name = tagItem.Model.Name;
            return;
        }

        // Re-position tag to maintain sorted order
        Tags.Remove(tagItem);
        var masterMatch = MasterTags.FirstOrDefault(t => t.Id == tagItem.Id);
        if (masterMatch != null) {
            masterMatch.Name = newName;
            MasterTags.Remove(masterMatch);
        }
        InsertTagSorted(tagItem);

        // Update any taxonomy node linked to this tag
        UpdateNodeNamesForTag(HierarchyNodes, tagItem.Id, newName);

        RefreshTagGroupViews();
        RefreshTagSuggestions();
        RefreshAvailableTaxonomyBranches();
        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
        OnPropertyChanged(nameof(SelectedNodeFullPath));
        OnPropertyChanged(nameof(SelectedTaxonomyPath));
    }

    private static int CountTagUsageInTree(IEnumerable<HierarchyNodeViewModel> nodes, Guid tagId, string name) {
        int count = 0;
        foreach (var node in nodes) {
            if (node.TagId == tagId || node.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                count++;
            }
            count += CountTagUsageInTree(node.Children, tagId, name);
        }
        return count;
    }

    private static void UpdateNodeNamesForTag(IEnumerable<HierarchyNodeViewModel> nodes, Guid tagId, string newName) {
        foreach (var node in nodes) {
            if (node.TagId == tagId) {
                node.Name = newName;
            }
            UpdateNodeNamesForTag(node.Children, tagId, newName);
        }
    }

    private static void UnlinkTagFromTree(IEnumerable<HierarchyNodeViewModel> nodes, Guid tagId) {
        foreach (var node in nodes) {
            if (node.TagId == tagId) {
                node.TagId = null;
            }
            UnlinkTagFromTree(node.Children, tagId);
        }
    }

    // ── SUB-TAB 2: TAXONOMY (HIERARCHY) ────────────────────────────────────
    public ObservableCollection<HierarchyNodeViewModel> HierarchyNodes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalculatedBreadcrumbPath))]
    [NotifyPropertyChangedFor(nameof(SelectedNodeFullPath))]
    [NotifyPropertyChangedFor(nameof(SelectedTaxonomyPath))]
    [NotifyPropertyChangedFor(nameof(CalculatedXmpPath))]
    [NotifyPropertyChangedFor(nameof(HasSelectedHierarchyNode))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTaxonomyNode))]
    [NotifyPropertyChangedFor(nameof(IsTaxonomyPathVisible))]
    private HierarchyNodeViewModel? _selectedHierarchyNode;

    public bool HasSelectedHierarchyNode => SelectedHierarchyNode != null;

    public string CalculatedBreadcrumbPath =>
        SelectedHierarchyNode != null && !string.IsNullOrWhiteSpace(SelectedHierarchyNode.Name)
            ? SelectedHierarchyNode.GetDisplayBreadcrumb()
            : "None";

    public string SelectedNodeFullPath => CalculatedBreadcrumbPath;

    public string CalculatedXmpPath =>
        SelectedHierarchyNode != null
            ? SelectedHierarchyNode.GetXmpPath()
            : string.Empty;

    [RelayCommand]
    public void AddRootNode() {
        var rootNode = new HierarchyNodeViewModel(string.Empty, null, null, OnNodeCommit, OnNodeCancel) {
            IsNewNode = true,
            IsNewUncommitted = true,
            IsEditing = true,
            EditingName = string.Empty
        };
        HierarchyNodes.Add(rootNode);
        SelectedHierarchyNode = rootNode;
    }

    [RelayCommand]
    public void AddSubNode(HierarchyNodeViewModel? targetParent = null) {
        var parent = targetParent ?? SelectedHierarchyNode;
        if (parent == null) return;

        parent.IsExpanded = true;
        var childNode = new HierarchyNodeViewModel(string.Empty, null, parent, OnNodeCommit, OnNodeCancel) {
            IsNewNode = true,
            IsNewUncommitted = true,
            IsEditing = true,
            EditingName = string.Empty
        };
        parent.Children.Add(childNode);
        SelectedHierarchyNode = childNode;
    }

    [RelayCommand]
    public void StartEditSelectedNode() {
        SelectedHierarchyNode?.StartEdit();
    }

    [RelayCommand]
    public void DeleteNode(HierarchyNodeViewModel? targetNode = null) {
        var node = targetNode ?? SelectedHierarchyNode;
        if (node == null) return;
        RemoveNode(node);
    }

    [RelayCommand]
    public void DeleteSelectedNode() {
        DeleteNode(SelectedHierarchyNode);
    }

    private void OnNodeCommit(HierarchyNodeViewModel node) {
        var trimmed = node.EditingName.Trim().ToLowerInvariant().Replace('/', '|');
        if (string.IsNullOrWhiteSpace(trimmed)) {
            if (node.IsNewUncommitted || node.IsNewNode) {
                RemoveNode(node);
            } else {
                node.EditingName = node.Name;
                node.IsEditing = false;
            }
            return;
        }

        var tag = EnsureTagInPool(trimmed);
        node.Name = trimmed;
        node.TagId = tag.Id;
        node.Model.Name = trimmed;
        node.Model.TagId = tag.Id;
        node.IsNewNode = false;
        node.IsNewUncommitted = false;
        node.IsEditing = false;

        RefreshTagSuggestions();
        RefreshAvailableTaxonomyBranches();
        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
        OnPropertyChanged(nameof(SelectedNodeFullPath));
        OnPropertyChanged(nameof(SelectedTaxonomyPath));
        OnPropertyChanged(nameof(HasSelectedTaxonomyNode));
        OnPropertyChanged(nameof(IsTaxonomyPathVisible));
        OnPropertyChanged(nameof(CalculatedXmpPath));
    }

    private void OnNodeCancel(HierarchyNodeViewModel node) {
        if (node.IsNewUncommitted || node.IsNewNode) {
            RemoveNode(node);
        } else {
            node.EditingName = node.Name;
            node.IsEditing = false;
        }
    }

    private void RemoveNode(HierarchyNodeViewModel node) {
        if (node.Parent != null) {
            node.Parent.Children.Remove(node);
            SelectedHierarchyNode = node.Parent;
        } else {
            HierarchyNodes.Remove(node);
            SelectedHierarchyNode = HierarchyNodes.FirstOrDefault();
        }

        RefreshAvailableTaxonomyBranches();
        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
        OnPropertyChanged(nameof(SelectedNodeFullPath));
        OnPropertyChanged(nameof(SelectedTaxonomyPath));
        OnPropertyChanged(nameof(HasSelectedTaxonomyNode));
        OnPropertyChanged(nameof(IsTaxonomyPathVisible));
        OnPropertyChanged(nameof(CalculatedXmpPath));
    }

    private Tag EnsureTagInPool(string name) {
        name = name.Trim().ToLowerInvariant();
        var existing = Tags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.Ordinal));
        if (existing != null) {
            return existing.Model;
        }

        var newTag = new Tag { Name = name };
        var tagItem = new TagItemViewModel(newTag, RequestDeleteTag, OnTagRenamed);
        InsertTagSorted(tagItem);

        RefreshTagGroupViews();
        RefreshTagSuggestions();
        return newTag;
    }

    // ── SUB-TAB 3: GROUPS (SINGLE-CONTAINER 2-LEVEL EXPANDABLE LIST) ───────
    public ObservableCollection<GroupTreeNodeViewModel> TagGroupTreeNodes { get; } = new();
    public ObservableCollection<TagGroupItemViewModel> TagGroups { get; } = new();

    public string GroupsHeaderTitle => $"GROUPS ({TagGroupTreeNodes.Count})";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedGroupNode))]
    [NotifyPropertyChangedFor(nameof(SelectedGroupPath))]
    [NotifyPropertyChangedFor(nameof(HasSelectedBreadcrumb))]
    [NotifyPropertyChangedFor(nameof(SelectedBreadcrumbPath))]
    private GroupTreeNodeViewModel? _selectedGroupTreeNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedGroupName))]
    [NotifyPropertyChangedFor(nameof(SelectedGroupNameHeader))]
    [NotifyPropertyChangedFor(nameof(SelectedGroupTagCountBadge))]
    [NotifyPropertyChangedFor(nameof(HasSelectedGroup))]
    private TagGroupItemViewModel? _selectedTagGroup;

    public bool HasSelectedGroup => SelectedTagGroup != null;

    public string SelectedGroupName =>
        SelectedTagGroup != null && !string.IsNullOrWhiteSpace(SelectedTagGroup.GroupName)
            ? SelectedTagGroup.GroupName
            : (SelectedGroupTreeNode != null ? SelectedGroupTreeNode.Name : string.Empty);

    public string SelectedGroupNameHeader =>
        !string.IsNullOrWhiteSpace(SelectedGroupName)
            ? $"TAGS IN GROUP: {SelectedGroupName}"
            : "TAGS IN GROUP: (None selected)";

    public string SelectedGroupTagCountBadge =>
        SelectedTagGroup != null ? SelectedTagGroup.TagCountBadge : "0 tags";

    [ObservableProperty]
    private string _newTagGroupName = string.Empty;

    [ObservableProperty]
    private string _newGroupTagInput = string.Empty;

    [ObservableProperty]
    private bool _isAddingGroupTag;

    public ObservableCollection<Tag> SelectedGroupTags { get; } = new();
    public ObservableCollection<string> TagSuggestions { get; } = new();
    public ObservableCollection<TaxonomyBranchItem> AvailableTaxonomyBranches { get; } = new();

    public bool HasTaxonomyBranches => AvailableTaxonomyBranches.Count > 0;

    public bool HasGroupTags => SelectedGroupTags.Count > 0;

    [ObservableProperty]
    private TaxonomyBranchItem? _selectedTaxonomyBranch;

    partial void OnSelectedTagGroupChanged(TagGroupItemViewModel? value) {
        IsAddingGroupTag = false;
        RefreshTagGroupViews();
        OnPropertyChanged(nameof(SelectedGroupTagCountBadge));
    }

    partial void OnSelectedGroupTreeNodeChanged(GroupTreeNodeViewModel? value) {
        if (value != null) {
            var groupName = value.IsGroup ? value.Name : value.ParentGroup?.Name;
            if (!string.IsNullOrEmpty(groupName)) {
                _selectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                OnPropertyChanged(nameof(SelectedTagGroup));
                RefreshTagGroupViews();
            }
        }
        OnPropertyChanged(nameof(HasSelectedGroupNode));
        OnPropertyChanged(nameof(SelectedGroupPath));
        OnPropertyChanged(nameof(HasSelectedBreadcrumb));
        OnPropertyChanged(nameof(SelectedBreadcrumbPath));
    }

    partial void OnSelectedTaxonomyBranchChanged(TaxonomyBranchItem? value) {
        if (value != null) {
            ImportTaxonomyBranchAsGroup(value);
        }
    }

    [RelayCommand]
    public void AddInlineGroup() {
        var newGroupNode = new GroupTreeNodeViewModel(
            isGroup: true,
            string.Empty,
            null,
            OnGroupTreeCommit,
            OnGroupTreeCancel,
            OnGroupTreeDelete,
            OnGroupTreeAddChildTag) {
            IsNewNode = true,
            IsNewUncommitted = true,
            IsEditing = true,
            EditingName = string.Empty
        };
        TagGroupTreeNodes.Add(newGroupNode);
        SyncLegacyTagGroups();
        SelectedGroupTreeNode = newGroupNode;
        SelectedTagGroup = TagGroups.LastOrDefault();
        if (SelectedTagGroup != null) {
            SelectedTagGroup.IsNewNode = true;
            SelectedTagGroup.IsNewUncommitted = true;
            SelectedTagGroup.IsEditing = true;
        }
        OnPropertyChanged(nameof(GroupsHeaderTitle));
        OnPropertyChanged(nameof(HasSelectedBreadcrumb));
        OnPropertyChanged(nameof(SelectedBreadcrumbPath));
    }

    [RelayCommand]
    public void AddTagToGroupNode(GroupTreeNodeViewModel? targetGroup = null) {
        var group = targetGroup ?? (SelectedGroupTreeNode?.IsGroup == true ? SelectedGroupTreeNode : SelectedGroupTreeNode?.ParentGroup);
        if (group == null && TagGroupTreeNodes.Count > 0) {
            group = TagGroupTreeNodes.First();
        }
        if (group == null) return;

        group.IsExpanded = true;
        var childNode = new GroupTreeNodeViewModel(
            isGroup: false,
            string.Empty,
            group,
            OnGroupTreeCommit,
            OnGroupTreeCancel,
            OnGroupTreeDelete) {
            IsNewNode = true,
            IsNewUncommitted = true,
            IsEditing = true,
            EditingName = string.Empty
        };
        group.Children.Add(childNode);
        SelectedGroupTreeNode = childNode;
        OnPropertyChanged(nameof(HasSelectedBreadcrumb));
        OnPropertyChanged(nameof(SelectedBreadcrumbPath));
    }

    [RelayCommand]
    public void DeleteGroupTreeNode(GroupTreeNodeViewModel? targetNode = null) {
        var node = targetNode ?? SelectedGroupTreeNode;
        if (node == null) return;
        OnGroupTreeDelete(node);
    }

    [RelayCommand]
    public void StartEditSelectedGroupTreeNode() {
        SelectedGroupTreeNode?.StartEdit();
        SelectedTagGroup?.StartEdit();
    }

    [RelayCommand]
    public void DeleteSelectedGroupTreeNode() {
        DeleteGroupTreeNode(SelectedGroupTreeNode);
    }

    private void OnGroupTreeCommit(GroupTreeNodeViewModel node) {
        var trimmed = node.EditingName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed)) {
            if (node.IsNewUncommitted || node.IsNewNode) {
                OnGroupTreeCancel(node);
            } else {
                node.EditingName = node.Name;
                node.IsEditing = false;
            }
            return;
        }

        if (node.IsGroup) {
            var duplicate = TagGroupTreeNodes.FirstOrDefault(g => g != node && g.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null) {
                if (node.IsNewUncommitted || node.IsNewNode) {
                    TagGroupTreeNodes.Remove(node);
                    SelectedGroupTreeNode = duplicate;
                } else {
                    node.EditingName = node.Name;
                    node.IsEditing = false;
                }
                return;
            }

            node.Name = trimmed;
            if (node.GroupModel != null) {
                node.GroupModel.GroupName = trimmed;
            } else {
                node.GroupModel = new TagGroup {
                    GroupName = trimmed,
                    TagIds = new ObservableCollection<Guid>(node.Children.Where(c => c.TagId.HasValue).Select(c => c.TagId!.Value))
                };
            }
            node.IsNewNode = false;
            node.IsNewUncommitted = false;
            node.IsEditing = false;
        } else if (node.IsTag && node.ParentGroup != null) {
            var parent = node.ParentGroup;
            var tag = EnsureTagInPool(trimmed);

            var duplicate = parent.Children.FirstOrDefault(c => c != node && c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null) {
                if (node.IsNewUncommitted || node.IsNewNode) {
                    parent.Children.Remove(node);
                    SelectedGroupTreeNode = duplicate;
                } else {
                    node.EditingName = node.Name;
                    node.IsEditing = false;
                }
                return;
            }

            node.Name = trimmed;
            node.TagId = tag.Id;
            node.TagModel = tag;
            if (parent.GroupModel != null && !parent.GroupModel.TagIds.Contains(tag.Id)) {
                parent.GroupModel.TagIds.Add(tag.Id);
            }
            node.IsNewNode = false;
            node.IsNewUncommitted = false;
            node.IsEditing = false;
        }

        SyncLegacyTagGroups();
        OnPropertyChanged(nameof(GroupsHeaderTitle));
        OnPropertyChanged(nameof(HasSelectedBreadcrumb));
        OnPropertyChanged(nameof(SelectedBreadcrumbPath));
        OnPropertyChanged(nameof(SelectedGroupPath));
    }

    private void OnGroupTreeCancel(GroupTreeNodeViewModel node) {
        if (node.IsNewUncommitted || node.IsNewNode) {
            if (node.IsGroup) {
                TagGroupTreeNodes.Remove(node);
                SelectedGroupTreeNode = TagGroupTreeNodes.FirstOrDefault();
            } else if (node.ParentGroup != null) {
                node.ParentGroup.Children.Remove(node);
                SelectedGroupTreeNode = node.ParentGroup;
            }
        } else {
            node.EditingName = node.Name;
            node.IsEditing = false;
        }
        SyncLegacyTagGroups();
        OnPropertyChanged(nameof(GroupsHeaderTitle));
        OnPropertyChanged(nameof(HasSelectedBreadcrumb));
        OnPropertyChanged(nameof(SelectedBreadcrumbPath));
        OnPropertyChanged(nameof(SelectedGroupPath));
    }

    private void OnGroupTreeDelete(GroupTreeNodeViewModel node) {
        if (node.IsGroup) {
            TagGroupTreeNodes.Remove(node);
            SelectedGroupTreeNode = TagGroupTreeNodes.FirstOrDefault();
        } else if (node.ParentGroup != null) {
            node.ParentGroup.Children.Remove(node);
            if (node.TagId.HasValue && node.ParentGroup.GroupModel != null) {
                node.ParentGroup.GroupModel.TagIds.Remove(node.TagId.Value);
            }
            SelectedGroupTreeNode = node.ParentGroup;
        }
        SyncLegacyTagGroups();
        OnPropertyChanged(nameof(GroupsHeaderTitle));
        OnPropertyChanged(nameof(HasSelectedBreadcrumb));
        OnPropertyChanged(nameof(SelectedBreadcrumbPath));
        OnPropertyChanged(nameof(SelectedGroupPath));
    }

    private void OnGroupTreeAddChildTag(GroupTreeNodeViewModel parentGroup) {
        AddTagToGroupNode(parentGroup);
    }

    private void SyncLegacyTagGroups() {
        TagGroups.Clear();
        foreach (var gNode in TagGroupTreeNodes) {
            var tagIds = new ObservableCollection<Guid>(gNode.Children.Where(c => c.TagId.HasValue).Select(c => c.TagId!.Value));
            var model = gNode.GroupModel ?? new TagGroup { GroupName = gNode.Name, TagIds = tagIds };
            model.GroupName = gNode.Name;
            model.TagIds = tagIds;
            gNode.GroupModel = model;
            TagGroups.Add(new TagGroupItemViewModel(model, DeleteTagGroupItem, RenameTagGroupItem, CancelTagGroupItem));
        }
        if (SelectedGroupTreeNode != null) {
            var gName = SelectedGroupTreeNode.IsGroup ? SelectedGroupTreeNode.Name : SelectedGroupTreeNode.ParentGroup?.Name;
            if (!string.IsNullOrEmpty(gName)) {
                SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupName.Equals(gName, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [RelayCommand]
    public void ImportBranch(TaxonomyBranchItem? branch) {
        if (branch != null) {
            ImportTaxonomyBranchAsGroup(branch);
        }
    }

    public void ImportTaxonomyBranchAsGroup(TaxonomyBranchItem branch) {
        var name = branch.Node.Name.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name)) return;

        var existingNode = TagGroupTreeNodes.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existingNode != null) {
            SelectedGroupTreeNode = existingNode;
            SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupName.Equals(name, StringComparison.OrdinalIgnoreCase));
            SelectedTaxonomyBranch = null;
            return;
        }

        var tagIds = CollectTagIdsFromNode(branch.Node, true);
        var model = new TagGroup {
            GroupName = name,
            TagIds = new ObservableCollection<Guid>(tagIds)
        };

        var groupNode = new GroupTreeNodeViewModel(model, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete, OnGroupTreeAddChildTag);
        foreach (var id in tagIds) {
            var tag = Tags.FirstOrDefault(t => t.Id == id)?.Model ?? MasterTags.FirstOrDefault(t => t.Id == id);
            if (tag != null) {
                groupNode.Children.Add(new GroupTreeNodeViewModel(tag, groupNode, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete));
            }
        }
        TagGroupTreeNodes.Add(groupNode);
        SyncLegacyTagGroups();
        SelectedGroupTreeNode = groupNode;
        SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupName.Equals(name, StringComparison.OrdinalIgnoreCase));
        SelectedTaxonomyBranch = null;
        OnPropertyChanged(nameof(GroupsHeaderTitle));
    }

    [RelayCommand]
    private void AddTagGroup() {
        var name = NewTagGroupName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name)) return;

        var existing = TagGroupTreeNodes.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) {
            SelectedGroupTreeNode = existing;
            SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupName.Equals(name, StringComparison.OrdinalIgnoreCase));
            NewTagGroupName = string.Empty;
            return;
        }

        var model = new TagGroup {
            GroupName = name,
            TagIds = new ObservableCollection<Guid>()
        };
        var groupNode = new GroupTreeNodeViewModel(model, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete, OnGroupTreeAddChildTag);
        TagGroupTreeNodes.Add(groupNode);
        SyncLegacyTagGroups();
        SelectedGroupTreeNode = groupNode;
        SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupName.Equals(name, StringComparison.OrdinalIgnoreCase));
        NewTagGroupName = string.Empty;
        OnPropertyChanged(nameof(GroupsHeaderTitle));
    }

    private void DeleteTagGroupItem(TagGroupItemViewModel item) {
        var match = TagGroupTreeNodes.FirstOrDefault(g => g.Name.Equals(item.GroupName, StringComparison.OrdinalIgnoreCase));
        if (match != null) {
            TagGroupTreeNodes.Remove(match);
        }
        TagGroups.Remove(item);
        if (SelectedTagGroup == item) {
            SelectedTagGroup = TagGroups.FirstOrDefault();
        }
        if (SelectedGroupTreeNode?.Name.Equals(item.GroupName, StringComparison.OrdinalIgnoreCase) == true) {
            SelectedGroupTreeNode = TagGroupTreeNodes.FirstOrDefault();
        }
        OnPropertyChanged(nameof(GroupsHeaderTitle));
    }

    private void CancelTagGroupItem(TagGroupItemViewModel item) {
        if (item.IsNewUncommitted || item.IsNewNode) {
            DeleteTagGroupItem(item);
        } else {
            item.EditingName = item.GroupName;
            item.IsEditing = false;
        }
    }

    private void RenameTagGroupItem(TagGroupItemViewModel item, string newName) {
        var duplicate = TagGroupTreeNodes.FirstOrDefault(g => !g.Name.Equals(item.GroupName, StringComparison.OrdinalIgnoreCase) && g.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null) {
            item.EditingName = item.GroupName;
            return;
        }
        var match = TagGroupTreeNodes.FirstOrDefault(g => g.Name.Equals(item.GroupName, StringComparison.OrdinalIgnoreCase));
        if (match != null) {
            match.Name = newName;
            if (match.GroupModel != null) match.GroupModel.GroupName = newName;
        }
        item.GroupName = newName;
        item.Model.GroupName = newName;
        OnPropertyChanged(nameof(SelectedGroupName));
        OnPropertyChanged(nameof(SelectedGroupNameHeader));
    }

    [RelayCommand]
    public void DeleteTagGroup(TagGroupItemViewModel? group) {
        var target = group ?? SelectedTagGroup;
        if (target == null) return;
        DeleteTagGroupItem(target);
    }

    [RelayCommand]
    public void StartEditSelectedGroup() {
        if (SelectedGroupTreeNode != null) {
            SelectedGroupTreeNode.StartEdit();
        }
        SelectedTagGroup?.StartEdit();
    }

    [RelayCommand]
    public void DeleteSelectedGroup() {
        if (SelectedGroupTreeNode != null) {
            DeleteGroupTreeNode(SelectedGroupTreeNode);
        }
        if (SelectedTagGroup != null) {
            DeleteTagGroupItem(SelectedTagGroup);
        }
    }

    [RelayCommand]
    public void StartAddGroupTag() {
        IsAddingGroupTag = true;
        NewGroupTagInput = string.Empty;
    }

    [RelayCommand]
    public void CancelAddGroupTag() {
        IsAddingGroupTag = false;
        NewGroupTagInput = string.Empty;
    }

    [RelayCommand]
    private void AddTagToSelectedGroup(string? tagNameParam) {
        var name = (tagNameParam ?? NewGroupTagInput).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name)) {
            IsAddingGroupTag = false;
            return;
        }

        var groupNode = SelectedGroupTreeNode?.IsGroup == true ? SelectedGroupTreeNode : (SelectedGroupTreeNode?.ParentGroup ?? TagGroupTreeNodes.FirstOrDefault());
        if (groupNode == null) return;

        var tag = EnsureTagInPool(name);
        var existingTagNode = groupNode.Children.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existingTagNode == null) {
            var childNode = new GroupTreeNodeViewModel(tag, groupNode, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete);
            groupNode.Children.Add(childNode);
            if (groupNode.GroupModel != null && !groupNode.GroupModel.TagIds.Contains(tag.Id)) {
                groupNode.GroupModel.TagIds.Add(tag.Id);
            }
            SyncLegacyTagGroups();
            RefreshTagGroupViews();
            OnPropertyChanged(nameof(SelectedGroupTagCountBadge));
        }

        NewGroupTagInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveTagFromGroup(Tag? tag) {
        if (tag == null) return;
        var groupNode = SelectedGroupTreeNode?.IsGroup == true ? SelectedGroupTreeNode : (SelectedGroupTreeNode?.ParentGroup ?? TagGroupTreeNodes.FirstOrDefault());
        if (groupNode == null) return;

        var matchChild = groupNode.Children.FirstOrDefault(c => c.TagId == tag.Id || c.Name.Equals(tag.Name, StringComparison.OrdinalIgnoreCase));
        if (matchChild != null) {
            groupNode.Children.Remove(matchChild);
        }
        if (groupNode.GroupModel != null) {
            groupNode.GroupModel.TagIds.Remove(tag.Id);
        }
        SyncLegacyTagGroups();
        RefreshTagGroupViews();
        OnPropertyChanged(nameof(SelectedGroupTagCountBadge));
    }

    [RelayCommand]
    public void CreateGroupFromTaxonomyNode(HierarchyNodeViewModel? node) {
        var targetNode = node ?? SelectedHierarchyNode;
        if (targetNode == null) return;

        var groupName = targetNode.Name.Trim().ToLowerInvariant();
        var existingNode = TagGroupTreeNodes.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        if (existingNode != null) {
            SelectedGroupTreeNode = existingNode;
            SelectGroupsTab();
            return;
        }

        var tagIds = CollectTagIdsFromNode(targetNode, true);
        var model = new TagGroup {
            GroupName = groupName,
            TagIds = new ObservableCollection<Guid>(tagIds)
        };

        var groupNode = new GroupTreeNodeViewModel(model, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete, OnGroupTreeAddChildTag);
        foreach (var id in tagIds) {
            var tag = Tags.FirstOrDefault(t => t.Id == id)?.Model ?? MasterTags.FirstOrDefault(t => t.Id == id);
            if (tag != null) {
                groupNode.Children.Add(new GroupTreeNodeViewModel(tag, groupNode, OnGroupTreeCommit, OnGroupTreeCancel, OnGroupTreeDelete));
            }
        }
        TagGroupTreeNodes.Add(groupNode);
        SelectedGroupTreeNode = groupNode;
        SyncLegacyTagGroups();
        OnPropertyChanged(nameof(GroupsHeaderTitle));
        SelectGroupsTab();

        try {
            if (Application.Current != null) {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Groups")
                    .WithContent($"Group '{groupName}' created from taxonomy branch with {tagIds.Count} tag(s).")
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        } catch {
            // Ignored
        }
    }

    private List<Guid> CollectTagIdsFromNode(HierarchyNodeViewModel node, bool includeSubtree) {
        var result = new HashSet<Guid>();
        CollectNodeTagsRecursive(node, result, includeSubtree);
        return result.ToList();
    }

    private void CollectNodeTagsRecursive(HierarchyNodeViewModel node, HashSet<Guid> set, bool includeSubtree) {
        var tag = EnsureTagInPool(node.Name);
        set.Add(tag.Id);
        node.TagId = tag.Id;

        if (includeSubtree) {
            foreach (var child in node.Children) {
                CollectNodeTagsRecursive(child, set, true);
            }
        }
    }

    private void RefreshAvailableTaxonomyBranches() {
        AvailableTaxonomyBranches.Clear();
        foreach (var root in HierarchyNodes) {
            PopulateBranchesRecursive(root, "");
        }
        OnPropertyChanged(nameof(HasTaxonomyBranches));
    }

    private void PopulateBranchesRecursive(HierarchyNodeViewModel node, string parentPath) {
        var path = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath} › {node.Name}";
        AvailableTaxonomyBranches.Add(new TaxonomyBranchItem {
            Node = node,
            DisplayPath = path
        });

        foreach (var child in node.Children) {
            PopulateBranchesRecursive(child, path);
        }
    }

    private void RefreshTagGroupViews() {
        SelectedGroupTags.Clear();
        var selectedGroup = SelectedTagGroup?.Model ?? (SelectedGroupTreeNode?.IsGroup == true ? SelectedGroupTreeNode.GroupModel : SelectedGroupTreeNode?.ParentGroup?.GroupModel);
        if (selectedGroup == null) {
            OnPropertyChanged(nameof(HasGroupTags));
            return;
        }

        var tagMap = Tags.ToDictionary(t => t.Id, t => t.Model);
        foreach (var id in selectedGroup.TagIds) {
            if (tagMap.TryGetValue(id, out var tag)) {
                SelectedGroupTags.Add(tag);
            }
        }
        OnPropertyChanged(nameof(HasGroupTags));
    }

    private void RefreshTagSuggestions() {
        TagSuggestions.Clear();
        foreach (var tag in Tags) {
            TagSuggestions.Add(tag.Name);
        }
    }
}
