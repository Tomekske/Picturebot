using System.Collections.ObjectModel;
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

namespace Picturebot.ViewModels;

public partial class ProcessingQueueViewModel : ViewModelBase,
    IRecipient<ProcessingProgressMessage>,
    IRecipient<ProcessingCompletedMessage>,
    IRecipient<CurationCompletedMessage> {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurationQueue _curationQueue;

    [ObservableProperty]
    private ObservableCollection<AlbumQueueItem> _queueItems = new();

    [ObservableProperty]
    private int _curationQueueCount;

    [ObservableProperty]
    private bool _hasCurationItems;

    public ProcessingQueueViewModel(IServiceScopeFactory scopeFactory, ICurationQueue curationQueue) {
        _scopeFactory = scopeFactory;
        _curationQueue = curationQueue;
        
        WeakReferenceMessenger.Default.RegisterAll(this);
        
        _ = LoadQueueAsync();
        UpdateCurationCount();
    }

    private void UpdateCurationCount() {
        CurationQueueCount = _curationQueue.Count;
        HasCurationItems = CurationQueueCount > 0;
    }

    public void Receive(CurationCompletedMessage message) {
        UpdateCurationCount();
    }

    public void Receive(ProcessingProgressMessage message) {
        // Simple refresh for now, could be optimized to update specific items
        _ = LoadQueueAsync();
        UpdateCurationCount();
    }

    public void Receive(ProcessingCompletedMessage message) {
        _ = LoadQueueAsync();
        UpdateCurationCount();
    }

    public async Task LoadQueueAsync() {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pendingAlbums = await context.Pictures
            .Where(p => p.ProcessingState == ProcessingState.Pending ||
                        p.ProcessingState == ProcessingState.Processing)
            .GroupBy(p => p.ParentId)
            .Select(g => new {
                AlbumId = g.Key,
                AlbumName = g.First().Parent != null ? g.First().Parent!.Name : "Unknown Album",
                PendingCount = g.Count()
            })
            .ToListAsync();

        QueueItems.Clear();
        foreach (var album in pendingAlbums) {
            QueueItems.Add(new AlbumQueueItem(album.AlbumName, album.PendingCount));
        }
    }

    [RelayCommand]
    private void CloseDialog() {
        MainWindow.DialogManager.DismissDialog();
    }
}

public record AlbumQueueItem(string Name, int PendingCount);
