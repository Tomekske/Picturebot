using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Main.Views;
using SukiUI.Controls;
using SukiUI.Dialogs;


namespace Main.ViewModels;

public partial class StatusBarViewModel : ViewModelBase
{
    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();

    [RelayCommand]
    private void OpenSettings()
    {
        Debug.WriteLine("Settings Command Triggered!");
        // SukiHost.ShowDialog(new SettingsView(), allowBackgroundClose: true);
        // MainWindow.DialogManager.CreateDialog().Dismiss().ByClickingBackground().TryShow();
        // SukiMainHost.ShowDialog(dialogContent, allowBackgroundClose: true);
        // SukiDialog.
// Access the static manager from MainWindow to show the dialog
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new SettingsDialog()) // Your custom UserControl
            .WithTitle("Settings") // Optional title
            .TryShow();
    }
}