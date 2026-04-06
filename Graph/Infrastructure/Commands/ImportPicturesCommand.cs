using System.IO.Abstractions;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Utilities;
using PictureWorker.Domain.Interfaces;
using SixLabors.ImageSharp;

namespace Graph.Infrastructure.Commands;

public class ImportPicturesCommand {
    private readonly IAlbumService _albumService;
    private readonly INodeService _nodeService;
    private readonly IFileSystem _fileSystem;
    private readonly IPictureAnalyzer _pictureAnalyzer;
    private readonly IPictureProcessor _pictureProcessor;
    private readonly FileGrouper _fileGrouper;

    private static readonly string[] RawExtensions = [".CR2", ".NEF", ".ARW", ".DNG", ".ORF", ".RAF"];
    private static readonly string[] JpgExtensions = [".JPG", ".JPEG"];

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

    public async Task ExecuteAsync(int? parentId, string albumName, string libraryPath, string sourcePath, IProgress<ImportProgress>? progress = null) {
        // Task 1: Create the Album (Sub-folders are created by modified AlbumService)
        var album = await _albumService.CreateAsync(parentId, albumName, libraryPath);
        var albumPath = _fileSystem.Path.Combine(libraryPath, album.Uuid);
        
        var rawsPath = _fileSystem.Path.Combine(albumPath, "RAWs");
        var jpgsPath = _fileSystem.Path.Combine(albumPath, "JPGs");
        var thumbnailsPath = _fileSystem.Path.Combine(albumPath, "Thumbnails");

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
            string? rawFile = group.FilePaths.FirstOrDefault(f => RawExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()));
            string? jpgFile = group.FilePaths.FirstOrDefault(f => JpgExtensions.Contains(_fileSystem.Path.GetExtension(f).ToUpperInvariant()));

            // Task 4: Collision Handling for naming
            var finalFileName = baseFileName;
            var counter = 1;
            while (_fileSystem.File.Exists(_fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg")) || 
                   _fileSystem.File.Exists(_fileSystem.Path.Combine(rawsPath, finalFileName + _fileSystem.Path.GetExtension(rawFile ?? "")))) {
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
            if (analysisFile == null) continue;

            var hashResult = await _pictureAnalyzer.CalculateHashAsync(analysisFile);
            var sharpnessResult = await _pictureAnalyzer.CalculateSharpnessAsync(analysisFile);
            
            // Task 3: IPictureProcessor to generate a preview (auto-oriented)
            // We use a max dimension of 2400 for previews to keep them high-quality but manageable
            var previewResult = await _pictureProcessor.GenerateProcessedImageAsync(analysisFile, 2400, 2400);
            if (previewResult is { IsError: false }) {
                importedJpgPath = _fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                using var stream = _fileSystem.File.OpenWrite(importedJpgPath);
                await previewResult.Value.SaveAsJpegAsync(stream);
            } else if (jpgFile != null) {
                // Fallback to simple copy if processing fails and we have a JPG
                importedJpgPath = _fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                _fileSystem.File.Copy(jpgFile, importedJpgPath);
            }
            
            // Task 3: IPictureProcessor to generate a thumbnail (auto-oriented)
            var thumbnailResult = await _pictureProcessor.GenerateProcessedImageAsync(analysisFile, 400, 400);
            if (thumbnailResult is { IsError: false }) {
                var thumbnailFile = _fileSystem.Path.Combine(thumbnailsPath, finalFileName + ".jpg");
                using var stream = _fileSystem.File.OpenWrite(thumbnailFile);
                await thumbnailResult.Value.SaveAsJpegAsync(stream);
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
        }
    }
}
