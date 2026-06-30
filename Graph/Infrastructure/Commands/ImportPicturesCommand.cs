using System.Diagnostics;
using System.IO.Abstractions;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Utilities;
using PictureWorker.Domain.Interfaces;
using Serilog;

namespace Graph.Infrastructure.Commands;

public class ImportPicturesCommand : IImportPicturesCommand {
    private static readonly string[] RawExtensions = SupportedImageExtensions.RawExtensions;
    private static readonly string[] JpgExtensions = SupportedImageExtensions.JpgExtensions;
    private readonly IAlbumService _albumService;
    private readonly IFileSystem _fileSystem;
    private readonly INodeService _nodeService;
    private readonly IPictureAnalyzer _pictureAnalyzer;
    private readonly IXmpService _xmpService;
    private readonly IPathService _pathService;

    public ImportPicturesCommand(
        IAlbumService albumService,
        INodeService nodeService,
        IFileSystem fileSystem,
        IPictureAnalyzer pictureAnalyzer,
        IXmpService xmpService,
        IPathService pathService) {
        _albumService = albumService;
        _nodeService = nodeService;
        _fileSystem = fileSystem;
        _pictureAnalyzer = pictureAnalyzer;
        _xmpService = xmpService;
        _pathService = pathService;
    }

    public async Task<Album> ExecuteAsync(int? parentId, string albumName, string libraryPath, string sourcePath,
        IProgress<ImportProgress>? progress = null) {
        var stopwatch = Stopwatch.StartNew();
        
        // Task 1: Create the Album
        var album = await _albumService.CreateAsync(parentId, albumName, libraryPath);
        var albumPath = _fileSystem.Path.Combine(libraryPath, album.Uuid);

        var rawsPath = _fileSystem.Path.Combine(albumPath, "RAWs");
        var jpgsPath = _fileSystem.Path.Combine(albumPath, "JPGs");
        var thumbnailsPath = _fileSystem.Path.Combine(albumPath, "Thumbnails");

        // Ensure directories exist
        _fileSystem.Directory.CreateDirectory(rawsPath);
        _fileSystem.Directory.CreateDirectory(jpgsPath);
        _fileSystem.Directory.CreateDirectory(thumbnailsPath);

        // Task 2: Pre-calculation & Caching Phase (Fast Metadata Only)
        var allFiles = _fileSystem.Directory.GetFiles(sourcePath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => {
                var fileName = _fileSystem.Path.GetFileName(f);
                return !fileName.StartsWith('.') &&
                       SupportedImageExtensions.AllExtensions.Contains(_fileSystem.Path.GetExtension(f)
                           .ToUpperInvariant());
            })
            .ToList();

        var preGroupedByName = allFiles.GroupBy(f => _fileSystem.Path.GetFileNameWithoutExtension(f)).ToList();
        var cachedDataList = new List<CachedPictureData>();

        var preProcessCount = 0;
        foreach (var pair in preGroupedByName) {
            preProcessCount++;
            progress?.Report(new ImportProgress(preProcessCount, preGroupedByName.Count,
                $"Analyzing metadata for {pair.Key}..."));

            // Prefer JPG for metadata, fallback to RAW
            var fileToInspect = pair.FirstOrDefault(f =>
                                 JpgExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()))
                             ?? pair.First();

            var timeResult = await _pictureAnalyzer.ExtractTimestamp(fileToInspect);
            
            var primaryDate = timeResult is { IsError: false }
                ? timeResult.Value
                : _fileSystem.File.GetCreationTime(fileToInspect);

            foreach (var file in pair) {
                cachedDataList.Add(new CachedPictureData {
                    FilePath = file,
                    PrimaryDate = primaryDate,
                    PHash = 0, // Will be calculated by Background Worker
                    Width = 0, // Will be calculated by Background Worker
                    Height = 0 // Will be calculated by Background Worker
                });
            }
        }

        // Task 3: Import Process (Copying Files Only)
        var filePairs = cachedDataList
            .GroupBy(f => _fileSystem.Path.GetFileNameWithoutExtension(f.FilePath))
            .ToList();

        var totalImageCount = filePairs.Count;
        var processedCount = 0;

        foreach (var pair in filePairs) {
            processedCount++;
            progress?.Report(new ImportProgress(processedCount, totalImageCount, $"Importing {pair.Key}"));

            var rawFile = pair.FirstOrDefault(f =>
                RawExtensions.Contains(_fileSystem.Path.GetExtension(f.FilePath).ToUpperInvariant()));
            var jpgFile = pair.FirstOrDefault(f =>
                JpgExtensions.Contains(_fileSystem.Path.GetExtension(f.FilePath).ToUpperInvariant()));

            var analysisFile = jpgFile ?? rawFile;
            if (analysisFile == null) {
                continue;
            }

            var cachedData = analysisFile;

            // Naming Convention
            var baseFileName = cachedData.PrimaryDate.ToString("yyyy-MM-dd_HH-mm-ss");

            var finalFileName = baseFileName;
            var counter = 1;

            while (_fileSystem.File.Exists(_fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg")) ||
                   _fileSystem.File.Exists(_fileSystem.Path.Combine(rawsPath,
                       finalFileName + _fileSystem.Path.GetExtension(rawFile?.FilePath ?? ""))) ||
                   await _nodeService.ExistsAsync(album.Id, finalFileName, NodeType.Picture)) {
                finalFileName = $"{baseFileName}_{counter++}";
            }

            // Copy RAW
            string? rawExtension = null;
            if (rawFile != null) {
                rawExtension = _fileSystem.Path.GetExtension(rawFile.FilePath);
                var importedRawPath = _fileSystem.Path.Combine(rawsPath, finalFileName + rawExtension);
                _fileSystem.File.Copy(rawFile.FilePath, importedRawPath);
            }

            // Copy Analysis File (JPG) to JPGs folder for background worker to use
            if (jpgFile != null) {
                var importedJpgPath = _fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                _fileSystem.File.Copy(jpgFile.FilePath, importedJpgPath);
            }

            // Persist to Database as PENDING
            var pictureNode = new Picture {
                Name = finalFileName,
                ParentId = album.Id,
                Type = NodeType.Picture,
                CapturedAt = cachedData.PrimaryDate,
                ProcessingState = ProcessingState.Pending,
                Extension = rawExtension
            };

            await _nodeService.CreateNodeAsync(pictureNode);

            pictureNode.Parent = album;
            _pathService.PopulatePaths(pictureNode);
            await _xmpService.SaveMetadataAsync(pictureNode);

            album.Children ??= new List<Node>();
            if (!album.Children.Contains(pictureNode)) {
                album.Children.Add(pictureNode);
            }
        }

        stopwatch.Stop();
        var elapsed = stopwatch.Elapsed;
        Log.Information("Imported album {AlbumName} in {Elapsed:hh\\:mm\\:ss}", albumName, elapsed);

        return album;
    }
}
