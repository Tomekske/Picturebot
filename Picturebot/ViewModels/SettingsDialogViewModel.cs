using System;
using System.IO;
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

        var settings = _settingsService.Current;
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
        RedLabelShortcut = !string.IsNullOrWhiteSpace(settings.RedLabelShortcut)
            ? settings.RedLabelShortcut
            : "Ctrl+NumPad1";
        OrangeLabelShortcut = !string.IsNullOrWhiteSpace(settings.OrangeLabelShortcut)
            ? settings.OrangeLabelShortcut
            : "Ctrl+NumPad2";
        YellowLabelShortcut = !string.IsNullOrWhiteSpace(settings.YellowLabelShortcut)
            ? settings.YellowLabelShortcut
            : "Ctrl+NumPad3";
        GreenLabelShortcut = !string.IsNullOrWhiteSpace(settings.GreenLabelShortcut)
            ? settings.GreenLabelShortcut
            : "Ctrl+NumPad4";
        BlueLabelShortcut = !string.IsNullOrWhiteSpace(settings.BlueLabelShortcut)
            ? settings.BlueLabelShortcut
            : "Ctrl+NumPad5";
        PinkLabelShortcut = !string.IsNullOrWhiteSpace(settings.PinkLabelShortcut)
            ? settings.PinkLabelShortcut
            : "Ctrl+NumPad6";
        PurpleLabelShortcut = !string.IsNullOrWhiteSpace(settings.PurpleLabelShortcut)
            ? settings.PurpleLabelShortcut
            : "Ctrl+NumPad7";
        NoneLabelShortcut = !string.IsNullOrWhiteSpace(settings.NoneLabelShortcut)
            ? settings.NoneLabelShortcut
            : "Ctrl+NumPad0";
        FullscreenShortcut =
            !string.IsNullOrWhiteSpace(settings.FullscreenShortcut) ? settings.FullscreenShortcut : "F";
        OpenInExplorerShortcut = !string.IsNullOrWhiteSpace(settings.OpenInExplorerShortcut)
            ? settings.OpenInExplorerShortcut
            : "O";
        Rating0Shortcut = !string.IsNullOrWhiteSpace(settings.Rating0Shortcut) ? settings.Rating0Shortcut : "NumPad0";
        Rating1Shortcut = !string.IsNullOrWhiteSpace(settings.Rating1Shortcut) ? settings.Rating1Shortcut : "NumPad1";
        Rating2Shortcut = !string.IsNullOrWhiteSpace(settings.Rating2Shortcut) ? settings.Rating2Shortcut : "NumPad2";
        Rating3Shortcut = !string.IsNullOrWhiteSpace(settings.Rating3Shortcut) ? settings.Rating3Shortcut : "NumPad3";
        Rating4Shortcut = !string.IsNullOrWhiteSpace(settings.Rating4Shortcut) ? settings.Rating4Shortcut : "NumPad4";
        Rating5Shortcut = !string.IsNullOrWhiteSpace(settings.Rating5Shortcut) ? settings.Rating5Shortcut : "NumPad5";
        CurationPickedShortcut = !string.IsNullOrWhiteSpace(settings.CurationPickedShortcut) ? settings.CurationPickedShortcut : "P";
        CurationRejectedShortcut = !string.IsNullOrWhiteSpace(settings.CurationRejectedShortcut) ? settings.CurationRejectedShortcut : "X";
        CurationNeutralShortcut = !string.IsNullOrWhiteSpace(settings.CurationNeutralShortcut) ? settings.CurationNeutralShortcut : "U";
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
    private async Task SaveSettings() {
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

        CloseDialog();
    }

    [RelayCommand]
    private void CloseDialog() {
        MainWindow.DialogManager.DismissDialog();
    }
}
