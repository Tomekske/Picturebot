using System;
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
using Main.Views;
using Serilog;
using SukiUI.Toasts;

namespace Main.ViewModels;

public partial class SettingsDialogViewModel : ViewModelBase {
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private int _clusterThreshold = 10;

    [ObservableProperty]
    private bool _launchFullScreen;

    [ObservableProperty]
    private string _libraryLocation = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightActive))]
    [NotifyPropertyChangedFor(nameof(IsDarkActive))]
    [NotifyPropertyChangedFor(nameof(IsSystemActive))]
    private int _themeIndex;

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
        ClusterThreshold = settings.GroupingThreshold;
        LaunchFullScreen = settings.LaunchMaximized;

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
        try {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow is not Window window) {
                return;
            }

            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                Title = "Select Library Location",
                AllowMultiple = false
            });

            if (folders.Count > 0) {
                LibraryLocation = folders[0].Path.LocalPath;
            }
        } catch (Exception ex) {
            Log.Error(ex, "Error browsing library location");
        }
    }

    [RelayCommand]
    private async Task SaveSettings() {
        if (_settingsService == null) {
            return;
        }

        var settings = new SettingsModel {
            LibraryPath = LibraryLocation,
            GroupingThreshold = ClusterThreshold,
            LaunchMaximized = LaunchFullScreen,
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
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();

        CloseDialog();
    }

    [RelayCommand]
    private void CloseDialog() {
        MainWindow.DialogManager.DismissDialog();
    }
}
