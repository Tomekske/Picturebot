using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Infrastructure.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Messages;
using Picturebot.Messages;
using Graph.Domain.Interfaces;
using Picturebot.Views;
using Avalonia.Threading;
using Serilog;

namespace Picturebot.ViewModels;

/// <summary>
///     The Processing Queue dialog: running imports, the curation sync, and every picture the worker still has to do,
///     in the order it takes them. Worker and import messages trigger a short, coalesced refresh from the database.
/// </summary>
public partial class ProcessingQueueViewModel : ViewModelBase,
    IRecipient<PictureProcessingStartedMessage>,
    IRecipient<PictureProcessedMessage>,
    IRecipient<ProcessingCompletedMessage>,
    IRecipient<ImportStatusMessage>,
    IRecipient<CurationCompletedMessage> {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurationQueue _curationQueue;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isLoading;
    private bool _reloadRequested;

    /// <summary>
    ///     Pictures waiting for or in processing; the ones in progress have <see cref="QueuePictureItem.IsProcessing" />.
    /// </summary>
    public ObservableCollection<QueuePictureItem> Pictures { get; } = new();

    public ObservableCollection<ImportActivityItem> Imports { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasQueue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailures))]
    private int _failedCount;

    [ObservableProperty]
    private int _curationQueueCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasCurationItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasLoaded;

    public bool HasImports => Imports.Count > 0;

    public bool HasFailures => FailedCount > 0;

    public bool IsEmpty => HasLoaded && !HasQueue && !HasImports && !HasCurationItems;

    public ProcessingQueueViewModel(IServiceScopeFactory scopeFactory, ICurationQueue curationQueue,
        ObservableCollection<ImportActivityItem> imports) {
        _scopeFactory = scopeFactory;
        _curationQueue = curationQueue;
        Imports = imports;
        Imports.CollectionChanged += OnImportsChanged;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => {
            _refreshTimer.Stop();
            _ = LoadQueueAsync();
            UpdateCurationCount();
        };

        WeakReferenceMessenger.Default.RegisterAll(this);

        _ = LoadQueueAsync();
        UpdateCurationCount();
    }

    public void Receive(PictureProcessingStartedMessage message) {
        RequestRefresh();
    }

    public void Receive(PictureProcessedMessage message) {
        if (message.Value.State == ProcessingState.Failed) {
            Dispatcher.UIThread.Post(() => FailedCount++);
        }

        RequestRefresh();
    }

    public void Receive(ProcessingCompletedMessage message) {
        // -1 is the worker's idle heartbeat (every few seconds); only refresh when an album actually finished.
        if (message.Value != -1) {
            RequestRefresh();
        }
    }

    public void Receive(ImportStatusMessage message) {
        // Every copied picture joins the queue.
        RequestRefresh();
    }

    public void Receive(CurationCompletedMessage message) {
        Dispatcher.UIThread.Post(UpdateCurationCount);
    }

    /// <summary>
    ///     Refreshes shortly after the first of a burst of messages, so four parallel workers don't each hit the database.
    /// </summary>
    private void RequestRefresh() {
        Dispatcher.UIThread.Post(() => {
            if (!_refreshTimer.IsEnabled) {
                _refreshTimer.Start();
            }
        });
    }

    private void OnImportsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        OnPropertyChanged(nameof(HasImports));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void UpdateCurationCount() {
        CurationQueueCount = _curationQueue.Count;
        HasCurationItems = CurationQueueCount > 0;
    }

    /// <summary>
    ///     Reads the queued pictures and updates the list in place, so rows don't flicker or lose their scroll position.
    ///     Calls that arrive while a load runs are folded into one more load.
    /// </summary>
    public async Task LoadQueueAsync() {
        if (_isLoading) {
            _reloadRequested = true;
            return;
        }

        _isLoading = true;
        try {
            do {
                _reloadRequested = false;

                List<QueuedPictureRow> rows;
                using (var scope = _scopeFactory.CreateScope()) {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    rows = await context.Pictures
                        .AsNoTracking()
                        .Where(p => p.ProcessingState == ProcessingState.Pending ||
                                    p.ProcessingState == ProcessingState.Processing)
                        // Same order the worker takes them in: album by album, then by id.
                        .OrderBy(p => p.ParentId)
                        .ThenBy(p => p.Id)
                        .Select(p => new QueuedPictureRow(
                            p.Id,
                            p.Name,
                            p.Parent != null ? p.Parent.Name : "Unknown album",
                            p.ProcessingState == ProcessingState.Processing))
                        .ToListAsync();
                }

                var queuedIds = rows.Select(r => r.Id).ToHashSet();
                foreach (var done in Pictures.Where(p => !queuedIds.Contains(p.Id)).ToList()) {
                    Pictures.Remove(done);
                }

                var shown = Pictures.ToDictionary(p => p.Id);
                foreach (var row in rows) {
                    if (shown.TryGetValue(row.Id, out var item)) {
                        item.IsProcessing = row.IsProcessing;
                    } else {
                        Pictures.Add(new QueuePictureItem(row.Id, row.Name, row.AlbumName) { IsProcessing = row.IsProcessing });
                    }
                }

                HasQueue = Pictures.Count > 0;
            } while (_reloadRequested);
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load the processing queue");
        } finally {
            _isLoading = false;
            HasLoaded = true;
        }
    }

    [RelayCommand]
    private void CloseDialog() {
        _refreshTimer.Stop();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        Imports.CollectionChanged -= OnImportsChanged;
        MainWindow.DialogManager.DismissDialog();
    }

    private record QueuedPictureRow(int Id, string Name, string AlbumName, bool IsProcessing);
}

public partial class QueuePictureItem(int id, string name, string albumName) : ObservableObject {
    public int Id { get; } = id;
    public string Name { get; } = name;
    public string AlbumName { get; } = albumName;

    [ObservableProperty]
    private bool _isProcessing;
}
