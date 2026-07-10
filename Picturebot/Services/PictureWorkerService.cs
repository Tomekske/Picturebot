using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Domain.Enums;
using Domain.Interfaces;
using Graph.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Picturebot.Messages;
using PictureWorker.Domain.Interfaces;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Picturebot.Services;

public class PictureWorkerService(
    IServiceScopeFactory scopeFactory,
    IPictureAnalyzer pictureAnalyzer,
    IPictureProcessor pictureProcessor,
    IFileSystem fileSystem)
    : BackgroundService {
    private const int MaxParallelism = 4;
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Log.Information("PictureWorkerService starting...");
        try {
            // 1. Recovery: Reset 'Processing' to 'Pending' on startup
            await ResetOrphanedRecordsAsync();

            // 2. Main Processing Loop
            while (!stoppingToken.IsCancellationRequested) {
                try {
                    var albumId = await GetNextAlbumIdWithPendingWorkAsync();

                    if (albumId == null) {
                        // Notify that everything is done
                        WeakReferenceMessenger.Default.Send(new ProcessingCompletedMessage(-1)); // -1 indicates all albums done
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }

                    await ProcessAlbumAsync(albumId.Value, stoppingToken);

                    // Explicitly yield control back to the Avalonia Dispatcher and other background tasks
                    // to prevent process-wide starvation during heavy album transitions.
                    await Task.Delay(1, stoppingToken);
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    Log.Error(ex, "Error in PictureWorkerService main loop");
                    await Task.Delay(10000, stoppingToken);
                }
            }
        } finally {
            Log.Information("PictureWorkerService background processor stopped.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        Log.Information("PictureWorkerService service stopping...");
        await base.StopAsync(cancellationToken);
        Log.Information("PictureWorkerService service stopped.");
    }

    private async Task ResetOrphanedRecordsAsync() {
        try {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var orphaned = await context.Pictures
                .Where(p => p.ProcessingState == ProcessingState.Processing)
                .ToListAsync();

            if (orphaned.Any()) {
                Log.Information("Found {Count} orphaned processing records. Resetting to Pending.", orphaned.Count);
                foreach (var pic in orphaned) {
                    pic.ProcessingState = ProcessingState.Pending;
                }

                await context.SaveChangesAsync();
            }
        } catch (Exception ex) {
            Log.Error(ex, "Failed to reset orphaned records");
        }
    }

    private async Task<int?> GetNextAlbumIdWithPendingWorkAsync() {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Pictures
            .Where(p => p.ProcessingState == ProcessingState.Pending)
            .OrderBy(p => p.ParentId)
            .Select(p => p.ParentId)
            .FirstOrDefaultAsync();
    }

    private async Task ProcessAlbumAsync(int albumId, CancellationToken ct) {
        Log.Information("Processing Album {AlbumId}", albumId);

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var pathService = scope.ServiceProvider.GetRequiredService<IPathService>();

        // 1. Initial State: Load all metadata once before entering the parallel loop
        var allPicturesInAlbum = await context.Pictures
            .Include(p => p.Parent)
            .Where(p => p.ParentId == albumId)
            .ToListAsync(ct);

        var totalCount = allPicturesInAlbum.Count;
        var initiallyCompleted = allPicturesInAlbum.Count(p => p.ProcessingState == ProcessingState.Completed);
        var pendingPictures = allPicturesInAlbum.Where(p => p.ProcessingState == ProcessingState.Pending).ToList();

        if (!pendingPictures.Any()) {
            WeakReferenceMessenger.Default.Send(new ProcessingCompletedMessage(albumId));
            return;
        }

        // 2. In-Memory Tracking: Use Interlocked to avoid DB CountAsync queries inside the loop
        var sessionProcessedCount = 0;
        var lastReportTicks = DateTime.UtcNow.Ticks;
        var reportIntervalTicks = TimeSpan.FromMilliseconds(500).Ticks;

        using var semaphore = new SemaphoreSlim(MaxParallelism);
        var tasks = pendingPictures.Select(async picture => {
            await semaphore.WaitAsync(ct);
            try {
                // Persistence: Keep DB updates surgical and dedicated to state changes
                await UpdateProcessingStateAsync(picture.Id, ProcessingState.Processing);

                var (success, errorMsg) = await ProcessPictureInternalAsync(picture, pathService, settingsService, ct);

                if (!success) {
                    await HandleProcessingFailureAsync(picture.Id, errorMsg);
                }

                // Increment in-memory counter
                var currentSessionTotal = Interlocked.Increment(ref sessionProcessedCount);
                var currentTotalCompleted = initiallyCompleted + currentSessionTotal;

                // 3. Throttled Reporting: Use local time check to avoid messenger saturation
                var nowTicks = DateTime.UtcNow.Ticks;
                if (currentTotalCompleted == totalCount ||
                    nowTicks - Volatile.Read(ref lastReportTicks) > reportIntervalTicks) {
                    Interlocked.Exchange(ref lastReportTicks, nowTicks);

                    WeakReferenceMessenger.Default.Send(new ProcessingProgressMessage(
                        new ProcessingProgress(albumId, currentTotalCompleted, totalCount, picture.Name)));
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                Log.Error(ex, "Failed to process picture {PictureId}", picture.Id);
                await HandleProcessingFailureAsync(picture.Id, ex.Message);
            } finally {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Final report to ensure 100% completion is captured
        WeakReferenceMessenger.Default.Send(new ProcessingCompletedMessage(albumId));
        Log.Information("Completed processing Album {AlbumId}", albumId);
    }


    private async Task<(bool Success, string? ErrorMessage)> ProcessPictureInternalAsync(Picture picture, IPathService pathService,
        ISettingsService settingsService, CancellationToken ct) {
        pathService.PopulatePaths(picture);

        if (picture.SubFolder == null) {
            var errMsg = $"SubFolder is null for Picture {picture.Id} ({picture.Name}). Ensure Parent (Album) is loaded.";
            Log.Error(errMsg);
            return (false, errMsg);
        }

        // 1. Identify source for analysis & processing
        // Prefer the imported JPG (in Preview folder), fallback to the original RAW
        string? analysisSource = null;
        if (fileSystem.File.Exists(picture.SubFolder.Preview)) {
            analysisSource = picture.SubFolder.Preview;
        } else if (fileSystem.File.Exists(picture.SubFolder.Raw)) {
            analysisSource = picture.SubFolder.Raw;
        }

        if (analysisSource == null) {
            var errMsg = $"No source file found for {picture.Name} (ID: {picture.Id}). Expected at {picture.SubFolder.Preview} or {picture.SubFolder.Raw}";
            Log.Warning(errMsg);
            return (false, errMsg);
        }

        Log.Information("Analyzing {Name} using {File}", picture.Name, analysisSource);

        // 2. Dimensions
        var dimResult = await pictureAnalyzer.GetDimensionsAsync(analysisSource);
        if (!dimResult.IsError) {
            picture.Width = dimResult.Value.Width;
            picture.Height = dimResult.Value.Height;
        } else {
            Log.Warning("Failed to get dimensions for {Name}: {Error}", picture.Name, dimResult.FirstError.Description);
        }

        // 3. Hash
        var hashResult = await pictureAnalyzer.CalculateHashAsync(analysisSource);
        if (!hashResult.IsError) {
            picture.Hash = hashResult.Value;
        } else {
            Log.Warning("Failed to calculate hash for {Name}: {Error}", picture.Name,
                hashResult.FirstError.Description);
        }

        // 4. Sharpness
        var sharpnessResult = await pictureAnalyzer.CalculateSharpnessAsync(analysisSource);
        if (!sharpnessResult.IsError) {
            picture.Sharpness = sharpnessResult.Value;
        } else {
            Log.Warning("Failed to calculate sharpness for {Name}: {Error}", picture.Name,
                sharpnessResult.FirstError.Description);
        }

        // 5. Preview Generation
        // We always try to generate it to ensure it's downsampled and correctly oriented
        var previewResult =
            await pictureProcessor.GenerateProcessedImageAsync(analysisSource, 2400, 2400, KnownResamplers.Lanczos3);
        if (!previewResult.IsError) {
            using (var image = previewResult.Value) {
                var previewPath = picture.SubFolder.Preview;
                fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(previewPath)!);
                
                // Use a temporary file if we are overwriting the source to avoid locks/corruption
                var useTemp = string.Equals(analysisSource, previewPath, StringComparison.OrdinalIgnoreCase);
                var writePath = useTemp ? previewPath + ".tmp" : previewPath;

                await using (var stream = fileSystem.File.Create(writePath)) {
                    await image.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 99 }, ct);
                }

                if (useTemp) {
                    fileSystem.File.Move(writePath, previewPath, true);
                }
            }

            Log.Debug("Generated preview for {Name}", picture.Name);
        } else {
            var errMsg = $"Failed to generate preview for {picture.Name}: {previewResult.FirstError.Description}";
            Log.Warning(errMsg);
            return (false, errMsg);
        }

        // 6. Thumbnail Generation
        var thumbResult =
            await pictureProcessor.GenerateProcessedImageAsync(analysisSource, 400, 400, KnownResamplers.Triangle);
        if (!thumbResult.IsError) {
            using (var image = thumbResult.Value) {
                var thumbPath = picture.SubFolder.Thumbnail;
                fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(thumbPath)!);
                
                await using var stream = fileSystem.File.Create(thumbPath);
                await image.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 75 }, ct);
            }

            Log.Debug("Generated thumbnail for {Name}", picture.Name);
        } else {
            var errMsg = $"Failed to generate thumbnail for {picture.Name}: {thumbResult.FirstError.Description}";
            Log.Warning(errMsg);
            return (false, errMsg);
        }


        // Persist updates
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbPic = await context.Pictures.FindAsync(new object[] { picture.Id }, ct);
        if (dbPic != null) {
            dbPic.Width = picture.Width;
            dbPic.Height = picture.Height;
            dbPic.Hash = picture.Hash;
            dbPic.Sharpness = picture.Sharpness;
            dbPic.ProcessingState = ProcessingState.Completed;
            dbPic.LastErrorMessage = null;
            await context.SaveChangesAsync(ct);
        }

        return (true, null);
    }

    private async Task UpdateProcessingStateAsync(int pictureId, ProcessingState state) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var picture = await context.Pictures.FindAsync(pictureId);
        if (picture != null) {
            picture.ProcessingState = state;
            if (state == ProcessingState.Completed) {
                picture.LastErrorMessage = null;
                Log.Information("Picture {Id} ({Name}) processed successfully.", picture.Id, picture.Name);
            }

            await context.SaveChangesAsync();
        }
    }

    private async Task HandleProcessingFailureAsync(int pictureId, string? error = null) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var picture = await context.Pictures.FindAsync(pictureId);
        if (picture != null) {
            picture.RetryCount++;
            picture.LastErrorMessage = error ?? "Unknown error";
            picture.ProcessingState =
                picture.RetryCount >= MaxRetries ? ProcessingState.Failed : ProcessingState.Pending;

            Log.Warning("Picture {Id} ({Name}) processing failed (Attempt {Count}). Error: {Error}",
                picture.Id, picture.Name, picture.RetryCount, picture.LastErrorMessage);

            await context.SaveChangesAsync();
        }
    }
}
