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
using Graph.Domain.Interfaces;
using Picturebot.Views;
using Serilog;
using SukiUI.Toasts;
using Microsoft.Extensions.DependencyInjection;

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

        // Master Tags
        MasterTags.Clear();
        foreach (var tag in settings.MasterTags) {
            MasterTags.Add(tag);
        }

        // Hierarchy Nodes
        HierarchyNodes.Clear();
        foreach (var node in settings.HierarchyNodes) {
            HierarchyNodes.Add(node);
        }

        // Tag Groups
        TagGroups.Clear();
        foreach (var group in settings.TagGroups) {
            TagGroups.Add(group);
        }

        SelectedTagGroup = TagGroups.FirstOrDefault(g => g.GroupId == settings.ActiveTagGroupId) ?? TagGroups.FirstOrDefault();

        ThemeIndex = settings.ThemeMode switch {
            ThemeMode.Light => 0,
            ThemeMode.Dark => 1,
            _ => 2
        };
    }

    [RelayCommand]
    private void SetLightTheme() {
        ThemeIndex = 0;
    }

    [RelayCommand]
    private void SetDarkTheme() {
        ThemeIndex = 1;
    }

    [RelayCommand]
    private void SetSystemTheme() {
        ThemeIndex = 2;
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

        MainWindow.ToastManager.CreateToast()
            .WithTitle("Settings")
            .WithContent("Your preferences have been updated successfully.")
            .Dismiss().ByClicking()
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();

        CloseDialog(parameter);
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
            MasterTags = MasterTags.ToList(),
            HierarchyNodes = HierarchyNodes.ToList(),
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
            MainWindow.DialogManager.DismissDialog();
        }
    }

    [RelayCommand]
    private void RevertToDefault() {
        var defaults = new SettingsModel();
        LoadSettingsFromModel(defaults);
    }

    // ── SECTION 1: MASTER TAGS POOL ───────────────────────────────────────────
    public ObservableCollection<Tag> MasterTags { get; } = new();

    [ObservableProperty]
    private Tag? _selectedMasterTag;

    [ObservableProperty]
    private string _newMasterTagName = string.Empty;

    [ObservableProperty]
    private string _renameMasterTagName = string.Empty;

    partial void OnSelectedMasterTagChanged(Tag? value) {
        RenameMasterTagName = value?.Name ?? string.Empty;
    }

    [RelayCommand]
    private void AddMasterTag() {
        var name = NewMasterTagName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!MasterTags.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) {
            var newTag = new Tag { Name = name };
            MasterTags.Add(newTag);
            SelectedMasterTag = newTag;
            RefreshTagGroupViews();
        }
        NewMasterTagName = string.Empty;
    }

    [RelayCommand]
    private void RenameMasterTag() {
        if (SelectedMasterTag == null) return;
        var newName = RenameMasterTagName.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        SelectedMasterTag.Name = newName;
        // Update any hierarchy nodes linked to this tag
        UpdateNodeNamesForTag(HierarchyNodes, SelectedMasterTag.Id, newName);

        // Trigger UI update by resetting selection
        var temp = SelectedMasterTag;
        SelectedMasterTag = null;
        SelectedMasterTag = temp;

        RefreshTagGroupViews();
        OnPropertyChanged(nameof(SelectedNodeXmpPath));
    }

    private static void UpdateNodeNamesForTag(IEnumerable<HierarchyNode> list, Guid tagId, string newName) {
        foreach (var node in list) {
            if (node.TagId == tagId) {
                node.Name = newName;
            }
            UpdateNodeNamesForTag(node.Children, tagId, newName);
        }
    }

    [RelayCommand]
    private void DeleteMasterTag() {
        if (SelectedMasterTag == null) return;
        var tagId = SelectedMasterTag.Id;

        // 1. Remove from MasterTags
        MasterTags.Remove(SelectedMasterTag);
        SelectedMasterTag = null;

        // 2. Unlink from HierarchyNodes
        UnlinkTagFromTree(HierarchyNodes, tagId);

        // 3. Remove from all TagGroups
        foreach (var group in TagGroups) {
            group.TagIds.Remove(tagId);
        }

        RefreshTagGroupViews();
        OnPropertyChanged(nameof(SelectedNodeXmpPath));
    }

    private static void UnlinkTagFromTree(IEnumerable<HierarchyNode> list, Guid tagId) {
        foreach (var node in list) {
            if (node.TagId == tagId) {
                node.TagId = null;
            }
            UnlinkTagFromTree(node.Children, tagId);
        }
    }

    // ── SECTION 2: HIERARCHY TAXONOMY ──────────────────────────────────────────
    public ObservableCollection<HierarchyNode> HierarchyNodes { get; } = new();

    [ObservableProperty]
    private HierarchyNode? _selectedHierarchyNode;

    [ObservableProperty]
    private string _newChildNodeName = string.Empty;

    [ObservableProperty]
    private Tag? _selectedSubNodeTag;

    partial void OnSelectedHierarchyNodeChanged(HierarchyNode? value) {
        OnPropertyChanged(nameof(SelectedNodeXmpPath));
    }

    public string SelectedNodeXmpPath {
        get {
            if (SelectedHierarchyNode == null) return string.Empty;
            return FindNodePath(HierarchyNodes, SelectedHierarchyNode, "") ?? SelectedHierarchyNode.Name;
        }
    }

    private static string? FindNodePath(IEnumerable<HierarchyNode> nodes, HierarchyNode target, string currentPath) {
        foreach (var node in nodes) {
            var path = string.IsNullOrEmpty(currentPath) ? node.Name : $"{currentPath}|{node.Name}";
            if (node == target) return path;
            var childResult = FindNodePath(node.Children, target, path);
            if (childResult != null) return childResult;
        }
        return null;
    }

    [RelayCommand]
    private void AddChildNode() {
        string name = SelectedSubNodeTag?.Name ?? NewChildNodeName.Trim().Replace('/', '|');
        if (string.IsNullOrWhiteSpace(name)) return;

        // Two-way sync: Ensure tag exists in MasterTags pool
        var tag = MasterTags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (tag == null) {
            tag = new Tag { Name = name };
            MasterTags.Add(tag);
            RefreshTagGroupViews();
        }

        var newNode = new HierarchyNode {
            Name = tag.Name,
            TagId = tag.Id
        };

        if (SelectedHierarchyNode != null) {
            SelectedHierarchyNode.Children.Add(newNode);
        } else {
            HierarchyNodes.Add(newNode);
        }

        NewChildNodeName = string.Empty;
        SelectedSubNodeTag = null;
        SelectedHierarchyNode = newNode;
        OnPropertyChanged(nameof(SelectedNodeXmpPath));
    }

    [RelayCommand]
    private void DeleteSelectedNode() {
        if (SelectedHierarchyNode == null) return;
        RemoveNodeFromTree(HierarchyNodes, SelectedHierarchyNode);
        SelectedHierarchyNode = null;
        OnPropertyChanged(nameof(SelectedNodeXmpPath));
    }

    private static bool RemoveNodeFromTree(IList<HierarchyNode> list, HierarchyNode target) {
        if (list.Remove(target)) return true;
        foreach (var item in list) {
            if (RemoveNodeFromTree(item.Children, target)) return true;
        }
        return false;
    }

    // ── SECTION 3: TAG GROUPS ──────────────────────────────────────────────────
    public ObservableCollection<TagGroup> TagGroups { get; } = new();

    [ObservableProperty]
    private TagGroup? _selectedTagGroup;

    [ObservableProperty]
    private string _newTagGroupName = string.Empty;

    [ObservableProperty]
    private Tag? _selectedTagToAddToGroup;

    public ObservableCollection<Tag> SelectedGroupTags { get; } = new();
    public ObservableCollection<Tag> AvailableTagsForGroup { get; } = new();

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
    private void DeleteTagGroup() {
        if (SelectedTagGroup == null) return;
        TagGroups.Remove(SelectedTagGroup);
        SelectedTagGroup = TagGroups.FirstOrDefault();
    }

    [RelayCommand]
    private void AddTagToGroup() {
        if (SelectedTagGroup == null || SelectedTagToAddToGroup == null) return;
        if (!SelectedTagGroup.TagIds.Contains(SelectedTagToAddToGroup.Id)) {
            SelectedTagGroup.TagIds.Add(SelectedTagToAddToGroup.Id);
            RefreshTagGroupViews();
        }
        SelectedTagToAddToGroup = null;
    }

    [RelayCommand]
    private void RemoveTagFromGroup(Tag? tag) {
        if (SelectedTagGroup == null || tag == null) return;
        SelectedTagGroup.TagIds.Remove(tag.Id);
        RefreshTagGroupViews();
    }

    private void RefreshTagGroupViews() {
        SelectedGroupTags.Clear();
        AvailableTagsForGroup.Clear();

        if (SelectedTagGroup == null) return;

        var tagMap = MasterTags.ToDictionary(t => t.Id);
        foreach (var id in SelectedTagGroup.TagIds) {
            if (tagMap.TryGetValue(id, out var tag)) {
                SelectedGroupTags.Add(tag);
            }
        }

        foreach (var tag in MasterTags) {
            if (!SelectedTagGroup.TagIds.Contains(tag.Id)) {
                AvailableTagsForGroup.Add(tag);
            }
        }
    }
}
