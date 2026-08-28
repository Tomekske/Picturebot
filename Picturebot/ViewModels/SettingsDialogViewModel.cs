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

    [ObservableProperty]
    private int _selectedTabIndex;

    // Sub-tab navigation in Keywords settings (0 = Tags, 1 = Taxonomy, 2 = Tag Groups)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTagsSubTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTaxonomySubTabActive))]
    [NotifyPropertyChangedFor(nameof(IsTagGroupsSubTabActive))]
    private int _keywordsSubTabIndex;

    public bool IsTagsSubTabActive => KeywordsSubTabIndex == 0;
    public bool IsTaxonomySubTabActive => KeywordsSubTabIndex == 1;
    public bool IsTagGroupsSubTabActive => KeywordsSubTabIndex == 2;

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

        // Tags (Catalog)
        Tags.Clear();
        MasterTags.Clear();
        foreach (var tag in settings.MasterTags) {
            MasterTags.Add(tag);
            Tags.Add(new TagItemViewModel(tag, RequestDeleteTag, OnTagRenamed));
        }

        // Taxonomy Nodes
        HierarchyNodes.Clear();
        foreach (var node in settings.HierarchyNodes) {
            HierarchyNodes.Add(new HierarchyNodeViewModel(node));
        }
        SelectedHierarchyNode = null;

        // Tag Groups
        TagGroups.Clear();
        foreach (var group in settings.TagGroups) {
            TagGroups.Add(group);
        }

        SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupId == settings.ActiveTagGroupId) ?? TagGroups.FirstOrDefault();
        RefreshTagSuggestions();

        ThemeIndex = settings.ThemeMode switch {
            ThemeMode.Light => 0,
            ThemeMode.Dark => 1,
            _ => 2
        };
    }

    [RelayCommand]
    private void SetLightTheme() => ThemeIndex = 0;

    [RelayCommand]
    private void SetDarkTheme() => ThemeIndex = 1;

    [RelayCommand]
    private void SetSystemTheme() => ThemeIndex = 2;

    [RelayCommand]
    private void SelectKeywordsSubTab(string? indexStr) {
        if (int.TryParse(indexStr, out var idx)) {
            KeywordsSubTabIndex = idx;
        }
    }

    [RelayCommand]
    private void SelectTagsSubTab() => KeywordsSubTabIndex = 0;

    [RelayCommand]
    private void SelectTaxonomySubTab() => KeywordsSubTabIndex = 1;

    [RelayCommand]
    private void SelectTagGroupsSubTab() => KeywordsSubTabIndex = 2;

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
            TagGroups = TagGroups.ToList(),
            ActiveTagGroupId = SelectedTagGroup?.GroupId,
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

    [RelayCommand]
    private void AddTag() {
        var name = NewTagName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!Tags.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) {
            var newTag = new Tag { Name = name };
            MasterTags.Add(newTag);
            var tagItem = new TagItemViewModel(newTag, RequestDeleteTag, OnTagRenamed);
            Tags.Add(tagItem);

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
            if (groupCount > 0) usageList.Add($"{groupCount} tag group(s)");

            DeleteTagConfirmMessage = $"This tag is currently used in {string.Join(" and ", usageList)}. Deleting it will unlink it from taxonomy and remove it from preset groups. Are you sure you want to proceed?";
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
        }

        RefreshTagGroupViews();
        RefreshTagSuggestions();
        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
    }

    private void OnTagRenamed(TagItemViewModel tagItem) {
        // Sync with MasterTags
        var masterMatch = MasterTags.FirstOrDefault(t => t.Id == tagItem.Id);
        if (masterMatch != null) {
            masterMatch.Name = tagItem.Name;
        }

        // Update any taxonomy node linked to this tag
        UpdateNodeNamesForTag(HierarchyNodes, tagItem.Id, tagItem.Name);

        RefreshTagGroupViews();
        RefreshTagSuggestions();
        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
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
    [NotifyPropertyChangedFor(nameof(CalculatedXmpPath))]
    [NotifyPropertyChangedFor(nameof(HasSelectedHierarchyNode))]
    private HierarchyNodeViewModel? _selectedHierarchyNode;

    public bool HasSelectedHierarchyNode => SelectedHierarchyNode != null;

    public string CalculatedBreadcrumbPath =>
        SelectedHierarchyNode != null
            ? SelectedHierarchyNode.GetDisplayBreadcrumb()
            : "No node selected";

    public string CalculatedXmpPath =>
        SelectedHierarchyNode != null
            ? SelectedHierarchyNode.GetXmpPath()
            : string.Empty;

    // Node action inline prompt state (Add Child / Add Root / Rename)
    [ObservableProperty]
    private bool _isNodeActionPromptOpen;

    [ObservableProperty]
    private string _nodeActionPromptTitle = string.Empty;

    [ObservableProperty]
    private string _nodeActionInputName = string.Empty;

    private enum NodeActionMode { AddChild, AddRoot, Rename }
    private NodeActionMode _currentActionMode;

    [RelayCommand]
    private void OpenAddChildNodePrompt() {
        if (SelectedHierarchyNode == null) return;
        _currentActionMode = NodeActionMode.AddChild;
        NodeActionPromptTitle = $"Add Sub-Node to '{SelectedHierarchyNode.Name}'";
        NodeActionInputName = string.Empty;
        IsNodeActionPromptOpen = true;
    }

    [RelayCommand]
    private void OpenAddRootNodePrompt() {
        _currentActionMode = NodeActionMode.AddRoot;
        NodeActionPromptTitle = "Add Root Category / Node";
        NodeActionInputName = string.Empty;
        IsNodeActionPromptOpen = true;
    }

    [RelayCommand]
    private void OpenRenameNodePrompt() {
        if (SelectedHierarchyNode == null) return;
        _currentActionMode = NodeActionMode.Rename;
        NodeActionPromptTitle = $"Rename Node '{SelectedHierarchyNode.Name}'";
        NodeActionInputName = SelectedHierarchyNode.Name;
        IsNodeActionPromptOpen = true;
    }

    [RelayCommand]
    private void ConfirmNodeAction() {
        var name = NodeActionInputName.Trim().Replace('/', '|');
        if (string.IsNullOrWhiteSpace(name)) {
            IsNodeActionPromptOpen = false;
            return;
        }

        switch (_currentActionMode) {
            case NodeActionMode.AddChild when SelectedHierarchyNode != null: {
                var tag = EnsureTagInPool(name);
                var childNode = new HierarchyNodeViewModel(tag.Name, tag.Id, SelectedHierarchyNode);
                SelectedHierarchyNode.Children.Add(childNode);
                SelectedHierarchyNode.IsExpanded = true;
                SelectedHierarchyNode = childNode;
                break;
            }
            case NodeActionMode.AddRoot: {
                var tag = EnsureTagInPool(name);
                var rootNode = new HierarchyNodeViewModel(tag.Name, tag.Id, null);
                HierarchyNodes.Add(rootNode);
                SelectedHierarchyNode = rootNode;
                break;
            }
            case NodeActionMode.Rename when SelectedHierarchyNode != null: {
                SelectedHierarchyNode.Name = name;
                // If linked to tag, update tag name too
                if (SelectedHierarchyNode.TagId.HasValue) {
                    var tag = Tags.FirstOrDefault(t => t.Id == SelectedHierarchyNode.TagId.Value);
                    if (tag != null) {
                        tag.Name = name;
                        tag.Model.Name = name;
                    }
                }
                break;
            }
        }

        IsNodeActionPromptOpen = false;
        NodeActionInputName = string.Empty;
        RefreshTagSuggestions();
        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
        OnPropertyChanged(nameof(CalculatedXmpPath));
    }

    [RelayCommand]
    private void CancelNodeAction() {
        IsNodeActionPromptOpen = false;
        NodeActionInputName = string.Empty;
    }

    [RelayCommand]
    private void DeleteSelectedNode() {
        if (SelectedHierarchyNode == null) return;
        var target = SelectedHierarchyNode;

        if (target.Parent != null) {
            target.Parent.Children.Remove(target);
            SelectedHierarchyNode = target.Parent;
        } else {
            HierarchyNodes.Remove(target);
            SelectedHierarchyNode = HierarchyNodes.FirstOrDefault();
        }

        OnPropertyChanged(nameof(CalculatedBreadcrumbPath));
        OnPropertyChanged(nameof(CalculatedXmpPath));
    }

    private Tag EnsureTagInPool(string name) {
        var existing = Tags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) {
            return existing.Model;
        }

        var newTag = new Tag { Name = name };
        MasterTags.Add(newTag);
        var tagItem = new TagItemViewModel(newTag, RequestDeleteTag, OnTagRenamed);
        Tags.Add(tagItem);
        RefreshTagGroupViews();
        RefreshTagSuggestions();
        return newTag;
    }

    // ── SUB-TAB 3: TAG GROUPS (PRESETS) ────────────────────────────────────
    public ObservableCollection<TagGroup> TagGroups { get; } = new();

    [ObservableProperty]
    private TagGroup? _selectedTagGroup;

    [ObservableProperty]
    private string _newTagGroupName = string.Empty;

    [ObservableProperty]
    private string _newGroupTagInput = string.Empty;

    public ObservableCollection<Tag> SelectedGroupTags { get; } = new();
    public ObservableCollection<string> TagSuggestions { get; } = new();

    partial void OnSelectedTagGroupChanged(TagGroup? value) {
        RefreshTagGroupViews();
    }

    [RelayCommand]
    private void AddTagGroup() {
        var name = NewTagGroupName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var newGroup = new TagGroup { GroupName = name };
        TagGroups.Add(newGroup);
        SelectedTagGroup = newGroup;
        NewTagGroupName = string.Empty;
    }

    [RelayCommand]
    private void DeleteTagGroup(TagGroup? group) {
        var target = group ?? SelectedTagGroup;
        if (target == null) return;

        TagGroups.Remove(target);
        if (SelectedTagGroup == target) {
            SelectedTagGroup = TagGroups.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void AddTagToSelectedGroup(string? tagNameParam) {
        if (SelectedTagGroup == null) return;

        var name = (tagNameParam ?? NewGroupTagInput).Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var tag = EnsureTagInPool(name);

        if (!SelectedTagGroup.TagIds.Contains(tag.Id)) {
            SelectedTagGroup.TagIds.Add(tag.Id);
            RefreshTagGroupViews();
        }

        NewGroupTagInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveTagFromGroup(Tag? tag) {
        if (SelectedTagGroup == null || tag == null) return;
        SelectedTagGroup.TagIds.Remove(tag.Id);
        RefreshTagGroupViews();
    }

    private void RefreshTagGroupViews() {
        SelectedGroupTags.Clear();
        if (SelectedTagGroup == null) return;

        var tagMap = Tags.ToDictionary(t => t.Id, t => t.Model);
        foreach (var id in SelectedTagGroup.TagIds) {
            if (tagMap.TryGetValue(id, out var tag)) {
                SelectedGroupTags.Add(tag);
            }
        }
    }

    private void RefreshTagSuggestions() {
        TagSuggestions.Clear();
        foreach (var tag in Tags) {
            TagSuggestions.Add(tag.Name);
        }
    }
}
