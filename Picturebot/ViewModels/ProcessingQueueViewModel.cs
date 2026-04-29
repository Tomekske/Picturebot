using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Infrastructure.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.Views;

namespace Picturebot.ViewModels;

public partial class ProcessingQueueViewModel : ViewModelBase {
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<AlbumQueueItem> _queueItems = new();

    public ProcessingQueueViewModel(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
        _ = LoadQueueAsync();
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
