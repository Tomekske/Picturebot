using System.IO.Abstractions;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Utilities;
using PictureWorker.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Graph.Infrastructure.Commands;

public class ImportPicturesCommand : IImportPicturesCommand {
    private static readonly string[] RawExtensions = SupportedImageExtensions.RawExtensions;
    private static readonly string[] JpgExtensions = SupportedImageExtensions.JpgExtensions;
    private readonly IAlbumService _albumService;
    private readonly FileGrouper _fileGrouper;
    private readonly IFileSystem _fileSystem;
    private readonly INodeService _nodeService;
    private readonly IPictureAnalyzer _pictureAnalyzer;
    private readonly IPictureProcessor _pictureProcessor;

    public ImportPicturesCommand(
        IAlbumService albumService,
        INodeService nodeService,
        IFileSystem fileSystem,
        IPictureAnalyzer pictureAnalyzer,
        IPictureProcessor pictureProcessor) {
        _albumService = albumService;
        _nodeService = nodeService;
        _fileSystem = fileSystem;
        _pictureAnalyzer = pictureAnalyzer;
        _pictureProcessor = pictureProcessor;

        // Updated: FileGrouper now only needs the file system as it operates on cached DTOs
        _fileGrouper = new FileGrouper(fileSystem);
    }

    public async Task<Album> ExecuteAsync(int? parentId, string albumName, string libraryPath, string sourcePath,
        IProgress<ImportProgress>? progress = null) {
        // Task 1: Create the Album
        var album = await _albumService.CreateAsync(parentId, albumName, libraryPath);
        var albumPath = _fileSystem.Path.Combine(libraryPath, album.Uuid);

        var rawsPath = _fileSystem.Path.Combine(albumPath, "RAWs");
        var jpgsPath = _fileSystem.Path.Combine(albumPath, "JPGs");
        var thumbnailsPath = _fileSystem.Path.Combine(albumPath, "Thumbnails");
        var pickedPath = _fileSystem.Path.Combine(albumPath, "Picked");

        // Ensure directories exist
        _fileSystem.Directory.CreateDirectory(rawsPath);
        _fileSystem.Directory.CreateDirectory(jpgsPath);
        _fileSystem.Directory.CreateDirectory(thumbnailsPath);

        // Task 2: Pre-calculation & Caching Phase
        var allFiles = _fileSystem.Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith('.') && !f.StartsWith("._"))
            .ToList();

        // Pair RAW+JPG by name early to avoid double-hashing the same picture
        var preGroupedByName = allFiles.GroupBy(f => _fileSystem.Path.GetFileNameWithoutExtension(f)).ToList();
        var cachedDataList = new List<CachedPictureData>(); // Assumes this DTO is defined in your domain

        var preProcessCount = 0;
        foreach (var pair in preGroupedByName) {
            preProcessCount++;
            progress?.Report(new ImportProgress(preProcessCount, preGroupedByName.Count,
                $"Analyzing metadata for {pair.Key}..."));

            // Prefer JPG for faster hashing, fallback to RAW
            var fileToHash = pair.FirstOrDefault(f =>
                                 JpgExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()))
                             ?? pair.First();

            var hashResult = await _pictureAnalyzer.CalculateHashAsync(fileToHash);
            var timeResult = await _pictureAnalyzer.ExtractTimestamp(fileToHash);

            if (hashResult.IsError) {
                continue;
            }

            var primaryDate = timeResult is { IsError: false }
                ? timeResult.Value
                : _fileSystem.File.GetCreationTime(fileToHash);

            // Add all files in this pair (RAW and JPG) to the cache list sharing the same hash and date
            foreach (var file in pair) {
                cachedDataList.Add(new CachedPictureData {
                    FilePath = file,
                    PrimaryDate = primaryDate,
                    PHash = hashResult.Value
                });
            }
        }

        // Task 3: Burst Grouping (In-Memory, O(N))
        var groups = _fileGrouper.GroupFiles(cachedDataList);

        // Count total unique images (pairs) across all burst groups for accurate progress reporting
        var totalImageCount = groups.Sum(g =>
            g.FilePaths.GroupBy(f => _fileSystem.Path.GetFileNameWithoutExtension(f)).Count());
        var processedCount = 0;

        // Task 4: Import Process
        foreach (var group in groups) {
            // A group might be a single photo, OR a burst containing multiple distinct photos.
            // We group by original base name again to identify the RAW+JPG pairs *within* the burst.
            var filePairsWithinBurst = group.FilePaths
                .GroupBy(f => _fileSystem.Path.GetFileNameWithoutExtension(f))
                .ToList();

            var burstIndex = 1;
            var isBurst = filePairsWithinBurst.Count > 1;

            foreach (var pair in filePairsWithinBurst) {
                processedCount++;
                progress?.Report(new ImportProgress(processedCount, totalImageCount, $"Importing {pair.Key}"));

                var rawFile = pair.FirstOrDefault(f =>
                    RawExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()));
                var jpgFile = pair.FirstOrDefault(f =>
                    JpgExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()));

                var analysisFile = jpgFile ?? rawFile;
                if (analysisFile == null) {
                    continue;
                }

                // We get the hash from the pre-calculation to save time
                var cachedData = cachedDataList.First(c => c.FilePath == analysisFile);
                var pHash = cachedData.PHash;

                // Check for duplicates before doing expensive processing
                if (await _nodeService.IsPictureHashDuplicateAsync(album.Id, pHash)) {
                    continue;
                }

                // Calculate sharpness here so we only run it on files that aren't duplicates
                var sharpnessResult = await _pictureAnalyzer.CalculateSharpnessAsync(analysisFile);
                if (sharpnessResult.IsError) {
                    continue;
                }

                // Naming Convention: Append a burst suffix if this is part of a high-speed sequence
                var baseFileName = cachedData.PrimaryDate.ToString("yyyy-MM-dd_HH-mm-ss");
                if (isBurst) {
                    baseFileName += $"_B{burstIndex++}";
                }

                // Collision Handling
                var finalFileName = baseFileName;
                var counter = 1;
                while (_fileSystem.File.Exists(_fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg")) ||
                       _fileSystem.File.Exists(_fileSystem.Path.Combine(rawsPath,
                           finalFileName + _fileSystem.Path.GetExtension(rawFile ?? "")))) {
                    finalFileName = $"{baseFileName}_{counter++}";
                }

                // Copy RAW
                if (rawFile != null) {
                    var extension = _fileSystem.Path.GetExtension(rawFile);
                    var importedRawPath = _fileSystem.Path.Combine(rawsPath, finalFileName + extension);
                    _fileSystem.File.Copy(rawFile, importedRawPath);
                }

                // Generate High-Fidelity Preview
                var previewResult =
                    await _pictureProcessor.GenerateProcessedImageAsync(analysisFile, 2400, 2400,
                        KnownResamplers.Lanczos3);
                if (previewResult is { IsError: false }) {
                    var importedJpgPath = _fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                    await using var stream = _fileSystem.File.OpenWrite(importedJpgPath);
                    await previewResult.Value.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 99 });
                } else if (jpgFile != null) {
                    var importedJpgPath = _fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                    _fileSystem.File.Copy(jpgFile, importedJpgPath);
                }

                // Generate Thumbnail
                var thumbnailResult =
                    await _pictureProcessor.GenerateProcessedImageAsync(analysisFile, 400, 400,
                        KnownResamplers.Triangle);
                if (thumbnailResult is { IsError: false }) {
                    var thumbnailFile = _fileSystem.Path.Combine(thumbnailsPath, finalFileName + ".jpg");
                    await using var stream = _fileSystem.File.OpenWrite(thumbnailFile);
                    await thumbnailResult.Value.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 75 });
                }

                // Persist to Database
                var pictureNode = new Picture {
                    Name = finalFileName,
                    ParentId = album.Id,
                    Type = NodeType.Picture,
                    CapturedAt = cachedData.PrimaryDate,
                    Hash = pHash,
                    Sharpness = sharpnessResult.Value
                };

                await _nodeService.CreateNodeAsync(pictureNode);

                album.Children ??= new List<Node>();
                if (!album.Children.Contains(pictureNode)) {
                    album.Children.Add(pictureNode);
                }
            }
        }

        return album;
    }
}
