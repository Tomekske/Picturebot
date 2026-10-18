using System.Collections.ObjectModel;
using System.Linq;
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
    IRecipient<ProcessingCompletedMessage>,
    IRecipient<ImportStatusMessage> {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurationQueue _curationQueue;

    // Last known processing state; imports take precedence over it in the status bar.
    private bool _isWorkerProcessing;
    private double _workerProgress;
    private string _workerStatusText = "Idle";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = "Idle";

    [ObservableProperty]
    private string? _statusToolTip;

    /// <summary>
    ///     Imports that are still running. Shared with the Processing Queue dialog.
    /// </summary>
    public ObservableCollection<ImportActivityItem> Imports { get; } = new();

    public SyncStatusViewModel(IServiceScopeFactory scopeFactory, ICurationQueue curationQueue) {
        _scopeFactory = scopeFactory;
        _curationQueue = curationQueue;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(ImportStatusMessage message) {
        Dispatcher.UIThread.Post(() => {
            var status = message.Value;
            var item = Imports.FirstOrDefault(i => i.Id == status.Id);

            if (status.Phase is ImportPhase.Completed or ImportPhase.Failed) {
                if (item != null) {
                    item.PropertyChanged -= OnImportChanged;
                    Imports.Remove(item);
                }
            } else {
                if (item == null) {
                    item = new ImportActivityItem(status.Id);
                    item.PropertyChanged += OnImportChanged;
                    Imports.Add(item);
                }

                item.Apply(status);
            }

            UpdateDisplay();
        });
    }

    public void Receive(ProcessingCompletedMessage message) {
        Dispatcher.UIThread.Post(() => {
            if (message.Value == -1) {
                _isWorkerProcessing = false;
                _workerProgress = 0;
                _workerStatusText = "Idle";
                UpdateDisplay();
            }
        });
    }

    public void Receive(ProcessingProgressMessage message) {
        Dispatcher.UIThread.Post(() => {
            var progress = message.Value;
            _isWorkerProcessing = true;

            if (progress.TotalCount > 0) {
                _workerProgress = (double)progress.ProcessedCount / progress.TotalCount * 100;
            }

            _workerStatusText = $"Processing {progress.ProcessedCount} of {progress.TotalCount}";
            UpdateDisplay();
        });
    }

    private void OnImportChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        UpdateDisplay();
    }

    private void UpdateDisplay() {
        // The newest import wins: it is the one the user just started.
        var import = Imports.LastOrDefault();
        if (import != null) {
            IsProcessing = true;
            IsIndeterminate = import.IsIndeterminate;
            ProgressValue = import.Percentage;
            StatusText = import.Phase == ImportPhase.Initializing
                ? import.Title
                : $"Importing {import.Processed} of {import.Total}";
            StatusToolTip = Imports.Count > 1
                ? $"{import.Title} — {import.Detail} (+{Imports.Count - 1} more)"
                : $"{import.Title} — {import.Detail}";
            return;
        }

        IsProcessing = _isWorkerProcessing;
        IsIndeterminate = false;
        ProgressValue = _workerProgress;
        StatusText = _workerStatusText;
        StatusToolTip = null;
    }

    [RelayCommand]
    private void ShowQueue() {
        Log.Information("Show Queue clicked");
        var vm = new ProcessingQueueViewModel(_scopeFactory, _curationQueue, Imports);
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new ProcessingQueueView { DataContext = vm })
            .TryShow();
    }
}
