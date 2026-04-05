using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using CommunityToolkit.Mvvm.Input;
using Main.Views;
using SukiUI.Dialogs;
using Domain.Enums;

namespace Main.ViewModels;

public partial class StatusBarViewModel : ViewModelBase {
    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();

    public string AppVersion { get; }
    public bool ShowDevBadge { get; }

    public StatusBarViewModel(IConfiguration configuration) {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        var env = configuration["Environment"] ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? AppEnvironment.Production;
        ShowDevBadge = env != AppEnvironment.Production;
    }

    [RelayCommand]
    private void OpenSettings() {
        Debug.WriteLine("Settings Command Triggered!");
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new SettingsDialog())
            .WithTitle("Settings")
            .TryShow();
    }
}
