using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.Messages;
using Picturebot.Views;
using SukiUI.Dialogs;

namespace Picturebot.ViewModels;

public partial class StatusBarViewModel : ViewModelBase, IRecipient<AiCurationStatusMessage> {
    public SyncStatusViewModel SyncStatusVM { get; }

    [ObservableProperty]
    private bool _isAiCurating;

    [ObservableProperty]
    private string _aiCurationStatusText = string.Empty;

    public StatusBarViewModel(IConfiguration configuration, IServiceScopeFactory scopeFactory, ICurationQueue curationQueue) {
        SyncStatusVM = new SyncStatusViewModel(scopeFactory, curationQueue);
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        var env = configuration["Environment"] ??
                  Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? AppEnvironment.Production;
        ShowDevBadge = env != AppEnvironment.Production;

        WeakReferenceMessenger.Default.Register<AiCurationStatusMessage>(this);
    }

    public ISukiDialogManager DialogManager { get; } = new SukiDialogManager();

    public string AppVersion { get; }
    public bool ShowDevBadge { get; }

    public void Receive(AiCurationStatusMessage message) {
        Dispatcher.UIThread.Post(() => {
            IsAiCurating = message.Value.IsCurating;
            AiCurationStatusText = message.Value.StatusText;
        });
    }

    [RelayCommand]
    private void OpenSettings() {
        Debug.WriteLine("Settings Command Triggered!");
        var dialog = new SettingsDialog();
        if (MainWindow.Instance != null) {
            dialog.ShowDialog(MainWindow.Instance);
        } else {
            dialog.Show();
        }
    }
}
