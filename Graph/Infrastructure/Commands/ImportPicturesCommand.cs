using System.IO.Abstractions;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Utilities;
using PictureWorker.Domain.Interfaces;
using SixLabors.ImageSharp;

namespace Graph.Infrastructure.Commands;

public class ImportPicturesCommand(
    IAlbumService albumService,
    INodeService nodeService,
    IFileSystem fileSystem,
    IPictureAnalyzer pictureAnalyzer,
    IPictureProcessor pictureProcessor) {
    
    private readonly FileGrouper _fileGrouper = new(fileSystem, pictureAnalyzer);

    private static readonly string[] RawExtensions = [".CR2", ".NEF", ".ARW", ".DNG", ".ORF", ".RAF"];
    private static readonly string[] JpgExtensions = [".JPG", ".JPEG"];

    public async Task ExecuteAsync(int? parentId, string albumName, string libraryPath, string sourcePath) {
        // Task 1: Create the Album (Sub-folders are created by modified AlbumService)
        var album = await albumService.CreateAsync(parentId, albumName, libraryPath);
        var albumPath = fileSystem.Path.Combine(libraryPath, album.Uuid);
        
        var rawsPath = fileSystem.Path.Combine(albumPath, "RAWs");
        var jpgsPath = fileSystem.Path.Combine(albumPath, "JPGs");
        var thumbnailsPath = fileSystem.Path.Combine(albumPath, "Thumbnails");

        // Task 2: Group files
        var groups = await _fileGrouper.GroupFilesAsync(sourcePath);

        foreach (var group in groups) {
            // Task 4: Implementation of naming convention
            var baseFileName = group.PrimaryDate.ToString("yyyy-MM-dd_HH-mm-ss");
            
            // Task 3: Classify files
            string? rawFile = group.FilePaths.FirstOrDefault(f => RawExtensions.Contains(fileSystem.Path.GetExtension(f).ToUpperInvariant()));
            string? jpgFile = group.FilePaths.FirstOrDefault(f => JpgExtensions.Contains(fileSystem.Path.GetExtension(f).ToUpperInvariant()));

            // Task 4: Collision Handling for naming
            var finalFileName = baseFileName;
            var counter = 1;
            while (fileSystem.File.Exists(fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg")) || 
                   fileSystem.File.Exists(fileSystem.Path.Combine(rawsPath, finalFileName + fileSystem.Path.GetExtension(rawFile ?? "")))) {
                finalFileName = $"{baseFileName}_{counter++}";
            }

            string? importedJpgPath = null;
            string? importedRawPath = null;
            
            if (jpgFile != null) {
                importedJpgPath = fileSystem.Path.Combine(jpgsPath, finalFileName + ".jpg");
                fileSystem.File.Copy(jpgFile, importedJpgPath);
            }
            
            if (rawFile != null) {
                var extension = fileSystem.Path.GetExtension(rawFile);
                importedRawPath = fileSystem.Path.Combine(rawsPath, finalFileName + extension);
                fileSystem.File.Copy(rawFile, importedRawPath);
            }

            // Task 3: IPictureAnalyzer to get metrics
            var analysisFile = jpgFile ?? rawFile;
            if (analysisFile == null) continue;

            var hashResult = await pictureAnalyzer.CalculateHashAsync(analysisFile);
            var sharpnessResult = await pictureAnalyzer.CalculateSharpnessAsync(analysisFile);
            
            // Task 3: IPictureProcessor to generate a thumbnail
            var thumbnailResult = await pictureProcessor.GenerateThumbnailAsync(analysisFile);
            if (thumbnailResult is { IsError: false }) {
                var thumbnailFile = fileSystem.Path.Combine(thumbnailsPath, finalFileName + ".jpg");
                using var stream = fileSystem.File.OpenWrite(thumbnailFile);
                await thumbnailResult.Value.SaveAsJpegAsync(stream);
            }

            // Task 3: Persists a new Picture node via INodeService
            var pictureNode = new Picture {
                Name = finalFileName,
                ParentId = album.Id,
                Type = NodeType.Picture,
                CapturedAt = group.PrimaryDate,
                Hash = hashResult.IsError ? 0 : hashResult.Value,
                Sharpness = sharpnessResult.IsError ? 0 : sharpnessResult.Value,
                // Add paths to the entity if it supports it
            };
            
            await nodeService.CreateNodeAsync(pictureNode);
        }
    }
}
