using System;
using System.Collections.Generic;
using System.IO;
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
using SixLabors.ImageSharp.Processing.Processors.Transforms;

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

        // 1. Recovery: Reset 'Processing' to 'Pending' on startup
        await ResetOrphanedRecordsAsync();

        // 2. Main Processing Loop
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var albumId = await GetNextAlbumIdWithPendingWorkAsync();

                if (albumId == null) {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                await ProcessAlbumAsync(albumId.Value, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                Log.Error(ex, "Error in PictureWorkerService main loop");
                await Task.Delay(10000, stoppingToken);
            }
        }
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

        var pictures = await context.Pictures
            .Include(p => p.Parent)
            .Where(p => p.ParentId == albumId && p.ProcessingState == ProcessingState.Pending)
            .ToListAsync(ct);

        if (!pictures.Any()) return;

        var totalCount = pictures.Count;
        var processedCount = 0;

        using var semaphore = new SemaphoreSlim(MaxParallelism);
        var tasks = pictures.Select(async picture => {
            await semaphore.WaitAsync(ct);
            try {
                // Mark as processing
                await UpdateProcessingStateAsync(picture.Id, ProcessingState.Processing);

                var success = await ProcessPictureInternalAsync(picture, pathService, settingsService, ct);

                if (success) {
                    await UpdateProcessingStateAsync(picture.Id, ProcessingState.Completed);
                } else {
                    await HandleProcessingFailureAsync(picture.Id);
                }

                var currentCount = Interlocked.Increment(ref processedCount);
                WeakReferenceMessenger.Default.Send(new ProcessingProgressMessage(
                    new ProcessingProgress(albumId, currentCount, totalCount, picture.Name)));

            } catch (Exception ex) {
                Log.Error(ex, "Failed to process picture {PictureId}", picture.Id);
                await HandleProcessingFailureAsync(picture.Id, ex.Message);
            } finally {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        
        WeakReferenceMessenger.Default.Send(new ProcessingCompletedMessage(albumId));
        Log.Information("Completed processing Album {AlbumId}", albumId);
    }

    private async Task<bool> ProcessPictureInternalAsync(Picture picture, IPathService pathService, ISettingsService settingsService, CancellationToken ct) {
        pathService.PopulatePaths(picture);
        
        if (picture.SubFolder == null) return false;

        // Determine analysis file (Prefer Preview if it exists, else RAW)
        // Actually, for analysis we want the RAW or imported high-res file.
        // During import, we copy raw to RAWs and preview to JPGs.
        var analysisFile = picture.SubFolder.Preview;
        if (!fileSystem.File.Exists(analysisFile)) {
            analysisFile = picture.SubFolder.Raw;
        }

        if (!fileSystem.File.Exists(analysisFile)) {
             // Try searching with extensions if Raw path is incomplete
             var rawDir = fileSystem.Path.GetDirectoryName(picture.SubFolder.Raw);
             if (rawDir != null && fileSystem.Directory.Exists(rawDir)) {
                 var files = fileSystem.Directory.GetFiles(rawDir, picture.Name + ".*");
                 analysisFile = files.FirstOrDefault() ?? analysisFile;
             }
        }

        if (!fileSystem.File.Exists(analysisFile)) {
            Log.Warning("Analysis file not found for {Name} at {Path}", picture.Name, analysisFile);
            return false;
        }

        // 1. Dimensions
        var dimResult = await pictureAnalyzer.GetDimensionsAsync(analysisFile);
        if (!dimResult.IsError) {
            picture.Width = dimResult.Value.Width;
            picture.Height = dimResult.Value.Height;
        }

        // 2. Hash
        var hashResult = await pictureAnalyzer.CalculateHashAsync(analysisFile);
        if (!hashResult.IsError) {
            picture.Hash = hashResult.Value;
        }

        // 3. Sharpness
        var sharpnessResult = await pictureAnalyzer.CalculateSharpnessAsync(analysisFile);
        if (!sharpnessResult.IsError) {
            picture.Sharpness = sharpnessResult.Value;
        }

        // 4. Preview Generation (If missing)
        if (!fileSystem.File.Exists(picture.SubFolder.Preview)) {
            var previewResult = await pictureProcessor.GenerateProcessedImageAsync(analysisFile, 2400, 2400, KnownResamplers.Lanczos3);
            if (!previewResult.IsError) {
                fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(picture.SubFolder.Preview)!);
                await using var stream = fileSystem.File.OpenWrite(picture.SubFolder.Preview);
                await previewResult.Value.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 99 }, ct);
            }
        }

        // 5. Thumbnail Generation (If missing)
        if (!fileSystem.File.Exists(picture.SubFolder.Thumbnail)) {
            var thumbResult = await pictureProcessor.GenerateProcessedImageAsync(analysisFile, 400, 400, KnownResamplers.Triangle);
            if (!thumbResult.IsError) {
                fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(picture.SubFolder.Thumbnail)!);
                await using var stream = fileSystem.File.OpenWrite(picture.SubFolder.Thumbnail);
                await thumbResult.Value.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 75 }, ct);
            }
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
            await context.SaveChangesAsync(ct);
        }

        return true;
    }

    private async Task UpdateProcessingStateAsync(int pictureId, ProcessingState state) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var picture = await context.Pictures.FindAsync(pictureId);
        if (picture != null) {
            picture.ProcessingState = state;
            if (state == ProcessingState.Completed) {
                 picture.LastErrorMessage = null;
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
            picture.LastErrorMessage = error;
            picture.ProcessingState = picture.RetryCount >= MaxRetries ? ProcessingState.Failed : ProcessingState.Pending;
            await context.SaveChangesAsync();
        }
    }
}
