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

public class KeywordNodeViewModel : ViewModelBase {
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<KeywordNodeViewModel> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
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

    [ObservableProperty]
    private int _selectedTabIndex;

    public SettingsDialogViewModel(ISettingsService settingsService) {
        _settingsService = settingsService;
        LoadSettings();
    }

    public SettingsDialogViewModel() {
        // Fallback or Designer constructor
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

        // Quick Tag Presets
        QuickTagPresetsList.Clear();
        var presets = (settings.QuickTagPresets ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in presets) QuickTagPresetsList.Add(p);

        // Global Keyword Taxonomy
        _taxonomyPaths.Clear();
        var paths = (settings.GlobalKeywordTaxonomy ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var path in paths) _taxonomyPaths.Add(path);

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

        var settings = new SettingsModel {
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
            QuickTagPresets = string.Join(";", QuickTagPresetsList),
            ThemeMode = ThemeIndex switch {
                0 => ThemeMode.Light,
                1 => ThemeMode.Dark,
                _ => ThemeMode.System
            }
        };

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
            QuickTagPresets = string.Join(";", QuickTagPresetsList),
            GlobalKeywordTaxonomy = string.Join(";", _taxonomyPaths),
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

    public ObservableCollection<KeywordNodeViewModel> GlobalKeywords { get; } = new();

    // Private list of all globally defined keyword taxonomy paths.
    // This is the canonical source of truth — independent of which album is open.
    private readonly List<string> _taxonomyPaths = new();

    // ── Quick Tag Presets ─────────────────────────────────────────────────────
    public ObservableCollection<string> QuickTagPresetsList { get; } = new();

    [ObservableProperty]
    private string _newPresetText = string.Empty;

    [RelayCommand]
    private void AddPreset() {
        var tag = NewPresetText.Trim();
        if (string.IsNullOrWhiteSpace(tag)) return;
        if (!QuickTagPresetsList.Contains(tag, StringComparer.OrdinalIgnoreCase)) {
            QuickTagPresetsList.Add(tag);
        }
        NewPresetText = string.Empty;
    }

    [RelayCommand]
    private void RemovePreset(string? tag) {
        if (tag != null) {
            QuickTagPresetsList.Remove(tag);
        }
    }

    [RelayCommand]
    private void MovePresetUp(string? tag) {
        if (tag == null) return;
        var idx = QuickTagPresetsList.IndexOf(tag);
        if (idx > 0) QuickTagPresetsList.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MovePresetDown(string? tag) {
        if (tag == null) return;
        var idx = QuickTagPresetsList.IndexOf(tag);
        if (idx >= 0 && idx < QuickTagPresetsList.Count - 1) QuickTagPresetsList.Move(idx, idx + 1);
    }

    // ── Keyword Taxonomy ─────────────────────────────────────────────────────

    [ObservableProperty]
    private KeywordNodeViewModel? _selectedKeywordNode;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private string _addKeywordText = string.Empty;

    partial void OnSelectedKeywordNodeChanged(KeywordNodeViewModel? value) {
        RenameText = value?.Name ?? string.Empty;
    }

    partial void OnSelectedTabIndexChanged(int value) {
        if (value == 5) { // Keywords tab
            LoadGlobalKeywords();
        }
    }

    public void LoadGlobalKeywords() {
        // Build a unified set: start from the stored global taxonomy,
        // then discover any keyword paths already on loaded pictures
        // (covers keywords added before this feature existed).
        var allPaths = new HashSet<string>(_taxonomyPaths, StringComparer.OrdinalIgnoreCase);

        bool discovered = false;
        foreach (var pic in GetLoadedPictures()) {
            foreach (var kw in pic.Keywords) {
                if (!string.IsNullOrWhiteSpace(kw) && allPaths.Add(kw)) {
                    discovered = true;
                }
            }
        }

        // Persist any newly discovered paths back into the taxonomy
        if (discovered) {
            _taxonomyPaths.Clear();
            _taxonomyPaths.AddRange(allPaths);
        }

        RebuildKeywordTree(allPaths);
    }

    private void RebuildKeywordTree(IEnumerable<string> paths) {
        GlobalKeywords.Clear();
        var roots = new List<KeywordNodeViewModel>();

        foreach (var path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)) {
            var segments = path.Split('|', StringSplitOptions.RemoveEmptyEntries);
            IList<KeywordNodeViewModel> currentList = roots;
            var currentPath = string.Empty;

            for (int i = 0; i < segments.Length; i++) {
                var segment = segments[i].Trim();
                currentPath = i == 0 ? segment : $"{currentPath}|{segment}";

                var existing = currentList.FirstOrDefault(n =>
                    n.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
                if (existing == null) {
                    existing = new KeywordNodeViewModel { Name = segment, FullPath = currentPath };
                    currentList.Add(existing);
                }
                currentList = existing.Children;
            }
        }

        foreach (var root in roots) GlobalKeywords.Add(root);
    }

    private async Task PersistTaxonomy() {
        if (_settingsService == null) return;
        await _settingsService.UpdateAsync(BuildCurrentSettingsModel());
    }

    private void SortKeywordNodes(IList<KeywordNodeViewModel> nodes) {
        var sorted = nodes.OrderBy(n => n.Name).ToList();
        nodes.Clear();
        foreach (var s in sorted) {
            nodes.Add(s);
            SortKeywordNodes(s.Children);
        }
    }

    private List<PictureItemViewModel> GetLoadedPictures() {
        if (MainWindow.Instance?.DataContext is MainWindowViewModel mainVm && mainVm.GalleryVM != null) {
            return mainVm.GalleryVM.AllPictures.ToList();
        }
        return new List<PictureItemViewModel>();
    }

    [RelayCommand]
    private async Task AddKeyword() {
        var raw = AddKeywordText.Trim().Replace('/', '|');
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (_taxonomyPaths.Contains(raw, StringComparer.OrdinalIgnoreCase)) {
            AddKeywordText = string.Empty;
            return;
        }

        _taxonomyPaths.Add(raw);
        AddKeywordText = string.Empty;
        LoadGlobalKeywords();

        // Auto-select the newly added leaf node
        var segments = raw.Split('|', StringSplitOptions.RemoveEmptyEntries);
        KeywordNodeViewModel? node = GlobalKeywords
            .FirstOrDefault(n => n.Name.Equals(segments[0], StringComparison.OrdinalIgnoreCase));
        for (int i = 1; i < segments.Length && node != null; i++) {
            node.IsExpanded = true;
            node = node.Children.FirstOrDefault(c =>
                c.Name.Equals(segments[i], StringComparison.OrdinalIgnoreCase));
        }
        SelectedKeywordNode = node;

        await PersistTaxonomy();
    }

    [RelayCommand]
    private async Task RenameKeyword() {
        if (SelectedKeywordNode == null || string.IsNullOrWhiteSpace(RenameText)) return;
        var oldPath = SelectedKeywordNode.FullPath;
        var parts = oldPath.Split('|');
        parts[parts.Length - 1] = RenameText.Trim();
        var newPath = string.Join("|", parts);

        // Update taxonomy paths
        for (int i = 0; i < _taxonomyPaths.Count; i++) {
            if (_taxonomyPaths[i].Equals(oldPath, StringComparison.OrdinalIgnoreCase))
                _taxonomyPaths[i] = newPath;
            else if (_taxonomyPaths[i].StartsWith(oldPath + "|", StringComparison.OrdinalIgnoreCase))
                _taxonomyPaths[i] = newPath + _taxonomyPaths[i].Substring(oldPath.Length);
        }

        // Rename in all loaded pictures
        var curationQueue = App.Services?.GetService<ICurationQueue>();
        foreach (var pic in GetLoadedPictures()) {
            bool changed = false;
            for (int i = 0; i < pic.Keywords.Count; i++) {
                var kw = pic.Keywords[i];
                if (kw.Equals(oldPath, StringComparison.OrdinalIgnoreCase)) {
                    pic.Keywords[i] = newPath; changed = true;
                } else if (kw.StartsWith(oldPath + "|", StringComparison.OrdinalIgnoreCase)) {
                    pic.Keywords[i] = newPath + kw.Substring(oldPath.Length); changed = true;
                }
            }
            if (changed) {
                pic.Picture.Keywords = pic.Keywords.ToList();
                pic.NotifyKeywordsChanged();
                curationQueue?.Enqueue(pic.Picture);
            }
        }

        LoadGlobalKeywords();
        RenameText = string.Empty;
        SelectedKeywordNode = null;
        await PersistTaxonomy();
    }

    [RelayCommand]
    private async Task DeleteKeyword() {
        if (SelectedKeywordNode == null) return;
        var pathToDelete = SelectedKeywordNode.FullPath;

        // Remove path and all child paths from the taxonomy
        _taxonomyPaths.RemoveAll(p =>
            p.Equals(pathToDelete, StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith(pathToDelete + "|", StringComparison.OrdinalIgnoreCase));

        // Remove from all loaded pictures
        var curationQueue = App.Services?.GetService<ICurationQueue>();
        foreach (var pic in GetLoadedPictures()) {
            var toRemove = pic.Keywords.Where(kw =>
                kw.Equals(pathToDelete, StringComparison.OrdinalIgnoreCase) ||
                kw.StartsWith(pathToDelete + "|", StringComparison.OrdinalIgnoreCase)).ToList();
            if (toRemove.Any()) {
                foreach (var kw in toRemove) pic.Keywords.Remove(kw);
                pic.Picture.Keywords = pic.Keywords.ToList();
                pic.NotifyKeywordsChanged();
                curationQueue?.Enqueue(pic.Picture);
            }
        }

        LoadGlobalKeywords();
        SelectedKeywordNode = null;
        await PersistTaxonomy();
    }
}
