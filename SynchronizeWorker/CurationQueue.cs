using System.Threading.Channels;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Synchronize;

public class CurationQueue : ICurationQueue, IHostedService, IDisposable {
    private readonly Channel<Picture> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private Task? _processTask;
    private CancellationTokenSource? _cts;

    public CurationQueue(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
        _channel = Channel.CreateUnbounded<Picture>();
    }

    public void Enqueue(Picture picture) {
        if (!_channel.Writer.TryWrite(picture)) {
            Log.Warning("Failed to enqueue picture {Name} for curation sync", picture.Name);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        Log.Information("CurationQueue service starting...");
        _cts = new CancellationTokenSource();
        _processTask = Task.Run(ProcessQueueAsync);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        Log.Information("CurationQueue service stopping, draining remaining {Count} items...", _channel.Reader.Count);
        _channel.Writer.TryComplete();
        if (_processTask != null) {
            // Wait for the processing task to complete or the shutdown timeout to be reached
            var delayTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completedTask = await Task.WhenAny(_processTask, delayTask);
            
            if (completedTask == delayTask) {
                Log.Warning("CurationQueue stop timed out. Some items may not have been persisted.");
            }
        }
    }

    private async Task ProcessQueueAsync() {
        // We do NOT pass a cancellation token to ReadAllAsync because we want to drain
        // the channel after the writer is completed during StopAsync.
        await foreach (var picture in _channel.Reader.ReadAllAsync()) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
                var pickedService = scope.ServiceProvider.GetRequiredService<IPickedService>();

                // 1. Adds the curated picture 'preview' to the database 
                // (Already updated in the Picture object by VM, here we persist it)
                await nodeService.UpdateNodeAsync(picture);

                // 2. copy to the 'Picked' folder
                await pickedService.SyncToPickedAsync(picture);
                
                Log.Information("Successfully synchronized curation for {Name}", picture.Name);
            } catch (Exception ex) {
                Log.Error(ex, "Error processing curation sync for {Name}", picture.Name);
            }
        }
    }

    public void Dispose() {
        _cts?.Cancel();
        _channel.Writer.TryComplete();
        _cts?.Dispose();
    }
}
