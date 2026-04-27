using System.Threading.Channels;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Serilog;

namespace Synchronize;

public class CurationQueue : ICurationQueue, IDisposable {
    private readonly Channel<Picture> _channel;
    private readonly IPickedService _pickedService;
    private readonly INodeService _nodeService;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processTask;

    public CurationQueue(IPickedService pickedService, INodeService nodeService) {
        _pickedService = pickedService;
        _nodeService = nodeService;
        _channel = Channel.CreateUnbounded<Picture>();
        _cts = new CancellationTokenSource();
        _processTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
    }

    public void Enqueue(Picture picture) {
        if (!_channel.Writer.TryWrite(picture)) {
            Log.Warning("Failed to enqueue picture {Name} for curation sync", picture.Name);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct) {
        await foreach (var picture in _channel.Reader.ReadAllAsync(ct)) {
            try {
                // 1. Adds the curated picture 'preview' to the database 
                // (Already updated in the Picture object by VM, here we persist it)
                await _nodeService.UpdateNodeAsync(picture);

                // 2. copy to the 'Picked' folder
                await _pickedService.SyncToPickedAsync(picture);
                
                Log.Information("Successfully synchronized curation for {Name}", picture.Name);
            } catch (Exception ex) {
                Log.Error(ex, "Error processing curation sync for {Name}", picture.Name);
            }
        }
    }

    public void Dispose() {
        _cts.Cancel();
        _channel.Writer.Complete();
        _cts.Dispose();
    }
}
