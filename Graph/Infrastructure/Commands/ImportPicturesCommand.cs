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
        _fileGrouper = new FileGrouper(fileSystem, pictureAnalyzer);
    }

    public async Task<Album> ExecuteAsync(int? parentId, string albumName, string libraryPath, string sourcePath,
        IProgress<ImportProgress>? progress = null) {
        // Task 1: Create the Album (Sub-folders are created by modified AlbumService)
        var album = await _albumService.CreateAsync(parentId, albumName, libraryPath);
        var albumPath = _fileSystem.Path.Combine(libraryPath, album.Uuid);

        var rawsPath = _fileSystem.Path.Combine(albumPath, "RAWs");
        var jpgsPath = _fileSystem.Path.Combine(albumPath, "JPGs");
        var thumbnailsPath = _fileSystem.Path.Combine(albumPath, "Thumbnails");
        var pickedPath = _fileSystem.Path.Combine(albumPath, "Picked");

        // Task 2: Group files
        var groups = await _fileGrouper.GroupFilesAsync(sourcePath);
        var totalCount = groups.Count;
        var processedCount = 0;

        foreach (var group in groups) {
            processedCount++;
            var currentFile = group.BaseName;
            progress?.Report(new ImportProgress(processedCount, totalCount, currentFile));

            // Task 4: Implementation of naming convention
            var baseFileName = group.PrimaryDate.ToString("yyyy-MM-dd_HH-mm-ss");

            // Task 3: Classify files
            var rawFile = group.FilePaths.FirstOrDefault(f =>
                RawExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()));
            var jpgFile = group.FilePaths.FirstOrDefault(f =>
                JpgExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()));

            // Task 4: Collision Handling for naming
            var finalFileName = baseFileName;
            var counter = 1;
            while (_fileSystem.File.Exists(_fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg")) ||
                   _fileSystem.File.Exists(_fileSystem.Path.Combine(rawsPath,
                       finalFileName + _fileSystem.Path.GetExtension(rawFile ?? "")))) {
                finalFileName = $"{baseFileName}_{counter++}";
            }

            string? importedJpgPath = null;
            string? importedRawPath = null;

            if (rawFile != null) {
                var extension = _fileSystem.Path.GetExtension(rawFile);
                importedRawPath = _fileSystem.Path.Combine(rawsPath, finalFileName + extension);
                _fileSystem.File.Copy(rawFile, importedRawPath);
            }

            // Task 3: IPictureAnalyzer to get metrics
            var analysisFile = jpgFile ?? rawFile;
            if (analysisFile == null) {
                continue;
            }

            var hashResult = await _pictureAnalyzer.CalculateHashAsync(analysisFile);
            var sharpnessResult = await _pictureAnalyzer.CalculateSharpnessAsync(analysisFile);

            // Abort if metadata extraction fails. We do not want 0-value records in the DB.
            if (hashResult.IsError || sharpnessResult.IsError) {
                continue;
            }

            // Prevent duplicate records for the same physical asset within the same album.
            if (await _nodeService.IsPictureHashDuplicateAsync(album.Id, hashResult.Value)) {
                continue;
            }

            // Task 3: IPictureProcessor to generate a preview (auto-oriented)
            // Preview Path (High Fidelity)
            var previewResult =
                await _pictureProcessor.GenerateProcessedImageAsync(analysisFile, 2400, 2400, KnownResamplers.Lanczos3);
            if (previewResult is { IsError: false }) {
                importedJpgPath = _fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                await using var stream = _fileSystem.File.OpenWrite(importedJpgPath);
                await previewResult.Value.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 99 });
            } else if (jpgFile != null) {
                // Fallback to simple copy if processing fails and we have a JPG
                importedJpgPath = _fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                _fileSystem.File.Copy(jpgFile, importedJpgPath);
            }

            // Task 3: IPictureProcessor to generate a thumbnail (auto-oriented)
            // Thumbnail Path (Fast Path)
            var thumbnailResult =
                await _pictureProcessor.GenerateProcessedImageAsync(analysisFile, 400, 400, KnownResamplers.Triangle);
            if (thumbnailResult is { IsError: false }) {
                var thumbnailFile = _fileSystem.Path.Combine(thumbnailsPath, finalFileName + ".jpg");
                await using var stream = _fileSystem.File.OpenWrite(thumbnailFile);
                await thumbnailResult.Value.SaveAsJpegAsync(stream, new JpegEncoder { Quality = 75 });
            }

            // Task 3: Persists a new Picture node via INodeService
            var pictureNode = new Picture {
                Name = finalFileName,
                ParentId = album.Id,
                Type = NodeType.Picture,
                CapturedAt = group.PrimaryDate,
                Hash = hashResult.IsError ? 0 : hashResult.Value,
                Sharpness = sharpnessResult.IsError ? 0 : sharpnessResult.Value
            };

            await _nodeService.CreateNodeAsync(pictureNode);
            
            // The service already set pictureNode.Parent = album, which might trigger EF fix-up
            // and add it to album.Children if they share the same context. 
            // We check to avoid doubling.
            album.Children ??= new List<Node>();
            if (!album.Children.Contains(pictureNode)) {
                album.Children.Add(pictureNode);
            }
        }

        return album;
    }
}
