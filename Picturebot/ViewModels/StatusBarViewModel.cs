using System;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Picturebot.Views;
using SukiUI.Dialogs;

using Microsoft.Extensions.DependencyInjection;

namespace Picturebot.ViewModels;

public partial class StatusBarViewModel : ViewModelBase {
    public SyncStatusViewModel SyncStatusVM { get; }

    public StatusBarViewModel(IConfiguration configuration, IServiceScopeFactory scopeFactory) {
        SyncStatusVM = new SyncStatusViewModel(scopeFactory);
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        var env = configuration["Environment"] ??
                  Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? AppEnvironment.Production;
        ShowDevBadge = env != AppEnvironment.Production;
    }

    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();

    public string AppVersion { get; }
    public bool ShowDevBadge { get; }

    [RelayCommand]
    private void OpenSettings() {
        Debug.WriteLine("Settings Command Triggered!");
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new SettingsDialog())
            .TryShow();
    }
}
