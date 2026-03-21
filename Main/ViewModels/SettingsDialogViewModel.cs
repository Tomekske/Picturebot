using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Main.Views;
using SukiUI;
using Avalonia;
using Avalonia.Styling;

namespace Main.ViewModels;

public partial class SettingsDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightActive))]
    [NotifyPropertyChangedFor(nameof(IsDarkActive))]
    [NotifyPropertyChangedFor(nameof(IsSystemActive))]
    private int _themeIndex = 0;

    public bool IsLightActive => ThemeIndex == 0;
    public bool IsDarkActive => ThemeIndex == 1;
    public bool IsSystemActive => ThemeIndex == 2;

    [ObservableProperty] private string _libraryLocation = string.Empty;

    [ObservableProperty] private int _clusterThreshold = 10;

    [ObservableProperty] private bool _launchFullScreen;

    [RelayCommand]
    private void SetLightTheme()
    {
        try
        {
            SukiTheme.GetInstance().ChangeBaseTheme(ThemeVariant.Light);
            ThemeIndex = 0;
        }
        catch
        {
            ThemeIndex = 0;
        }
    }

    [RelayCommand]
    private void SetDarkTheme()
    {
        try
        {
            SukiTheme.GetInstance().ChangeBaseTheme(ThemeVariant.Dark);
            ThemeIndex = 1;
        }
        catch
        {
            ThemeIndex = 1;
        }
    }

    [RelayCommand]
    private void SetSystemTheme()
    {
        try
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Default;
            }

            ThemeIndex = 2;
        }
        catch
        {
            ThemeIndex = 2;
        }
    }

    [RelayCommand]
    private void BrowseLibraryLocation()
    {
        // logic for folder picker could be added here
    }

    [RelayCommand]
    private void CloseDialog()
    {
        MainWindow.DialogManager.DismissDialog();
    }
}