using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Main.Views;
using SukiUI.Dialogs;

namespace Main.ViewModels;

public partial class StatusBarViewModel : ViewModelBase {
    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();

    [RelayCommand]
    private void OpenSettings() {
        Debug.WriteLine("Settings Command Triggered!");
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new SettingsDialog())
            .WithTitle("Settings")
            .TryShow();
    }
}
