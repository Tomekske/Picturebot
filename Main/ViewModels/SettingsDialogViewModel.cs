using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Main.Views;
using SukiUI.Controls;

namespace Main.ViewModels;

public partial class SettingsDialogViewModel : ViewModelBase
{
    
    [RelayCommand]
    private void CancelSettings()
    {
        MainWindow.DialogManager.DismissDialog();
    }
    
    [RelayCommand]
    private void SaveSettings()
    {
        MainWindow.DialogManager.DismissDialog();
    }
}