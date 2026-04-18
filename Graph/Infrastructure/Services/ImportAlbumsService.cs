using System.IO.Abstractions;
using Database.Domain.Entities;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Graph.Infrastructure.Utilities;

namespace Graph.Infrastructure.Services;

public class ImportAlbumsService(
    IFileSystem fileSystem,
    IFolderService folderService,
    IImportPicturesCommand importPicturesCommand
) : IImportAlbumsService {
    public async Task ImportRecursiveAsync(int? parentId, string sourcePath, string libraryPath, IProgress<ImportBatchProgress>? progress = null) {
        var totalAlbums = CountAlbumsRecursive(sourcePath);
        var processedAlbums = 0;
        await ProcessDirectoryAsync(parentId, sourcePath, libraryPath, progress, totalAlbums, processedAlbums);
    }

    private int CountAlbumsRecursive(string path) {
        int count = DirectoryContainsImages(path) ? 1 : 0;
        try {
            foreach (var subDir in fileSystem.Directory.GetDirectories(path)) {
                count += CountAlbumsRecursive(subDir);
            }
        } catch {
            // Skip directories we can't access
        }
        return count;
    }

    private async Task<int> ProcessDirectoryAsync(int? parentId, string currentPath, string libraryPath, IProgress<ImportBatchProgress>? progress, int totalAlbums, int processedAlbums) {
        bool hasImages = DirectoryContainsImages(currentPath);
        
        // Trim trailing slashes to ensure GetFileName safely extracts the leaf folder
        var cleanPath = currentPath.TrimEnd(
            fileSystem.Path.DirectorySeparatorChar,
            fileSystem.Path.AltDirectorySeparatorChar
        );
        
        string nodeName = fileSystem.Path.GetFileName(cleanPath);
        if (string.IsNullOrWhiteSpace(nodeName)) {
            nodeName = currentPath; // Fallback for root drives
        }

        Node createdNode;
        if (hasImages) {
            processedAlbums++;
            var albumProgress = new Progress<ImportProgress>(p => {
                progress?.Report(new ImportBatchProgress(processedAlbums, totalAlbums, nodeName, p));
            });
            progress?.Report(new ImportBatchProgress(processedAlbums, totalAlbums, nodeName));
            createdNode = await importPicturesCommand.ExecuteAsync(parentId, nodeName, libraryPath, currentPath, albumProgress);
        } else {
            createdNode = await folderService.CreateAsync(parentId, nodeName);
        }

        try {
            foreach (var subDir in fileSystem.Directory.GetDirectories(currentPath)) {
                processedAlbums = await ProcessDirectoryAsync(createdNode.Id, subDir, libraryPath, progress, totalAlbums, processedAlbums);
            }
        } catch {
            // Skip directories we can't access
        }

        return processedAlbums;
    }

    private bool DirectoryContainsImages(string path) {
        try {
            return fileSystem.Directory.EnumerateFiles(path)
                .Any(f => SupportedImageExtensions.AllExtensions.Contains(fileSystem.Path.GetExtension(f).ToUpperInvariant()));
        } catch {
            return false;
        }
    }
}
