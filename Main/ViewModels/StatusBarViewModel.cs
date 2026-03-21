using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Main.Views;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Main.ViewModels;

public partial class StatusBarViewModel : ViewModelBase
{
    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();

    [RelayCommand]
    private void OpenSettings()
    {
        Debug.WriteLine("Settings Command Triggered!");
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new SettingsDialog())
            .WithTitle("Settings")
            .WithActionButton("Save", (obj) =>
            {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Settings")
                    .WithContent("Settings saved successfully!")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
                MainWindow.DialogManager.DismissDialog();
            })
            .WithActionButton("Cancel", (obj) => { MainWindow.DialogManager.DismissDialog(); })
            .TryShow();
    }
}