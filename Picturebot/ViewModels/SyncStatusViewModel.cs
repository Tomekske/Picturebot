using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Graph.Domain.Interfaces;
using Picturebot.Messages;
using Picturebot.Views;
using Serilog;
using SukiUI.Dialogs;
using Avalonia.Threading;
using WeakReferenceMessenger = CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger;

namespace Picturebot.ViewModels;

public partial class SyncStatusViewModel : ViewModelBase,
    IRecipient<ProcessingProgressMessage>,
    IRecipient<ProcessingCompletedMessage> {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurationQueue _curationQueue;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _pendingAlbumsCount;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = "Idle";

    public SyncStatusViewModel(IServiceScopeFactory scopeFactory, ICurationQueue curationQueue) {
        _scopeFactory = scopeFactory;
        _curationQueue = curationQueue;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(ProcessingCompletedMessage message) {
        Dispatcher.UIThread.Post(() => {
            if (message.Value == -1) {
                IsProcessing = false;
                ProgressValue = 0;
                StatusText = "Idle";
            }
        });
    }

    public void Receive(ProcessingProgressMessage message) {
        Dispatcher.UIThread.Post(() => {
            IsProcessing = true;
            var progress = message.Value;

            if (progress.TotalCount > 0) {
                ProgressValue = (double)progress.ProcessedCount / progress.TotalCount * 100;
            }

            StatusText = $"Processing {progress.CurrentItemName}";
        });
    }

    [RelayCommand]
    private void ShowQueue() {
        Log.Information("Show Queue clicked");
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new ProcessingQueueView {
                DataContext = new ProcessingQueueViewModel(_scopeFactory, _curationQueue)
            })
            .TryShow();
    }
}
