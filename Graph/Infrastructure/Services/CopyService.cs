using Database.Domain.Entities;
using Domain.Interfaces;
using Graph.Domain.Interfaces;
using Serilog;
using System.IO.Abstractions;

namespace Graph.Infrastructure.Services;

public class CopyService(IFileSystem fileSystem, IPathService pathService, ISettingsService settingsService) : ICopyService {
    public async Task<bool> CopyToEditAsync(Picture picture) {
        var editPath = settingsService.Current.EditFolderPath;
        if (string.IsNullOrWhiteSpace(editPath)) {
            Log.Warning("Edit destination folder is not configured.");
            return false;
        }

        if (picture.SubFolder == null) {
            pathService.PopulatePaths(picture);
        }

        var sourceFile = picture.SubFolder?.Raw;
        if (string.IsNullOrEmpty(sourceFile) || !fileSystem.File.Exists(sourceFile)) {
            Log.Warning("RAW file not found: {SourceFile}", sourceFile);
            return false;
        }

        return await CopyFileAsync(sourceFile, editPath);
    }

    public async Task<bool> CopyToPrintAsync(Picture picture) {
        var printPath = settingsService.Current.PrintFolderPath;
        if (string.IsNullOrWhiteSpace(printPath)) {
            Log.Warning("Print destination folder is not configured.");
            return false;
        }

        if (picture.SubFolder == null) {
            pathService.PopulatePaths(picture);
        }

        var sourceFile = picture.SubFolder?.Preview;
        if (string.IsNullOrEmpty(sourceFile) || !fileSystem.File.Exists(sourceFile)) {
            Log.Warning("JPG file not found: {SourceFile}", sourceFile);
            return false;
        }

        return await CopyFileAsync(sourceFile, printPath);
    }

    private async Task<bool> CopyFileAsync(string sourceFile, string destinationFolder) {
        try {
            if (!fileSystem.Directory.Exists(destinationFolder)) {
                fileSystem.Directory.CreateDirectory(destinationFolder);
            }

            var fileName = fileSystem.Path.GetFileName(sourceFile);
            var destinationFile = fileSystem.Path.Combine(destinationFolder, fileName);

            if (fileSystem.File.Exists(destinationFile)) {
                Log.Information("File already exists in destination: {DestinationFile}", destinationFile);
                return false; // Skip copying, signal duplicate
            }

            await Task.Run(() => fileSystem.File.Copy(sourceFile, destinationFile));
            Log.Information("Successfully copied {FileName} to {DestinationFolder}", fileName, destinationFolder);
            return true;
        } catch (Exception ex) {
            Log.Error(ex, "Error copying file {SourceFile} to {DestinationFolder}", sourceFile, destinationFolder);
            throw;
        }
    }
}
