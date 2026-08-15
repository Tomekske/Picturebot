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
            ThemeMode = ThemeIndex switch {
                0 => ThemeMode.Light,
                1 => ThemeMode.Dark,
                _ => ThemeMode.System
            }
        };

        await _settingsService.UpdateAsync(settings);

        MainWindow.ToastManager.CreateToast()
            .WithTitle("Settings")
            .WithContent("Your preferences have been updated successfully.")
            .Dismiss().ByClicking()
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();

        CloseDialog(parameter);
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

    [ObservableProperty]
    private KeywordNodeViewModel? _selectedKeywordNode;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private string _mergeTargetText = string.Empty;

    partial void OnSelectedKeywordNodeChanged(KeywordNodeViewModel? value) {
        if (value != null) {
            RenameText = value.Name;
        } else {
            RenameText = string.Empty;
        }
    }

    partial void OnSelectedTabIndexChanged(int value) {
        if (value == 5) { // Keywords tab index
            LoadGlobalKeywords();
        }
    }

    public void LoadGlobalKeywords() {
        GlobalKeywords.Clear();
        var allPictures = GetLoadedPictures();
        if (!allPictures.Any()) return;

        var allTags = allPictures.SelectMany(p => p.Keywords).Distinct().ToList();
        var roots = new List<KeywordNodeViewModel>();

        foreach (var tag in allTags) {
            var segments = tag.Split('|', StringSplitOptions.RemoveEmptyEntries);
            IList<KeywordNodeViewModel> currentList = roots;
            string currentPath = "";

            for (int i = 0; i < segments.Length; i++) {
                var segment = segments[i].Trim();
                currentPath = i == 0 ? segment : $"{currentPath}|{segment}";

                var existingNode = currentList.FirstOrDefault(n => n.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
                if (existingNode == null) {
                    existingNode = new KeywordNodeViewModel {
                        Name = segment,
                        FullPath = currentPath
                    };
                    currentList.Add(existingNode);
                }
                currentList = existingNode.Children;
            }
        }

        SortKeywordNodes(roots);

        foreach (var r in roots) {
            GlobalKeywords.Add(r);
        }
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
    private void RenameKeyword() {
        if (SelectedKeywordNode == null || string.IsNullOrWhiteSpace(RenameText)) return;
        var oldPath = SelectedKeywordNode.FullPath;
        var newName = RenameText.Trim();

        var parts = oldPath.Split('|');
        parts[parts.Length - 1] = newName;
        var newPath = string.Join("|", parts);

        var allPictures = GetLoadedPictures();
        var curationQueue = App.Services?.GetService<ICurationQueue>();

        foreach (var pic in allPictures) {
            bool changed = false;
            for (int i = 0; i < pic.Keywords.Count; i++) {
                var kw = pic.Keywords[i];
                if (kw.Equals(oldPath, StringComparison.OrdinalIgnoreCase)) {
                    pic.Keywords[i] = newPath;
                    changed = true;
                } else if (kw.StartsWith(oldPath + "|", StringComparison.OrdinalIgnoreCase)) {
                    pic.Keywords[i] = newPath + kw.Substring(oldPath.Length);
                    changed = true;
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
    }

    [RelayCommand]
    private void MergeKeywords() {
        if (SelectedKeywordNode == null || string.IsNullOrWhiteSpace(MergeTargetText)) return;
        var sourcePath = SelectedKeywordNode.FullPath;
        var targetPath = MergeTargetText.Trim().Replace('/', '|');

        var allPictures = GetLoadedPictures();
        var curationQueue = App.Services?.GetService<ICurationQueue>();

        foreach (var pic in allPictures) {
            bool changed = false;
            var newKeywords = new List<string>();

            foreach (var kw in pic.Keywords) {
                if (kw.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)) {
                    if (!newKeywords.Contains(targetPath, StringComparer.OrdinalIgnoreCase)) {
                        newKeywords.Add(targetPath);
                    }
                    changed = true;
                } else if (kw.StartsWith(sourcePath + "|", StringComparison.OrdinalIgnoreCase)) {
                    var childTargetPath = targetPath + kw.Substring(sourcePath.Length);
                    if (!newKeywords.Contains(childTargetPath, StringComparer.OrdinalIgnoreCase)) {
                        newKeywords.Add(childTargetPath);
                    }
                    changed = true;
                } else {
                    newKeywords.Add(kw);
                }
            }

            if (changed) {
                pic.Keywords.Clear();
                foreach (var kw in newKeywords) {
                    pic.Keywords.Add(kw);
                }
                pic.Picture.Keywords = pic.Keywords.ToList();
                pic.NotifyKeywordsChanged();
                curationQueue?.Enqueue(pic.Picture);
            }
        }

        LoadGlobalKeywords();
        MergeTargetText = string.Empty;
        SelectedKeywordNode = null;
    }

    [RelayCommand]
    private void DeleteKeyword() {
        if (SelectedKeywordNode == null) return;
        var pathToDelete = SelectedKeywordNode.FullPath;

        var allPictures = GetLoadedPictures();
        var curationQueue = App.Services?.GetService<ICurationQueue>();

        foreach (var pic in allPictures) {
            var toRemove = pic.Keywords.Where(kw => 
                kw.Equals(pathToDelete, StringComparison.OrdinalIgnoreCase) || 
                kw.StartsWith(pathToDelete + "|", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (toRemove.Any()) {
                foreach (var kw in toRemove) {
                    pic.Keywords.Remove(kw);
                }
                pic.Picture.Keywords = pic.Keywords.ToList();
                pic.NotifyKeywordsChanged();
                curationQueue?.Enqueue(pic.Picture);
            }
        }

        LoadGlobalKeywords();
        SelectedKeywordNode = null;
    }
}
