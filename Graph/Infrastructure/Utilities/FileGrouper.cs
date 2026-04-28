using System.IO.Abstractions;
using Graph.Domain.Models;
using PictureWorker.Domain.Interfaces;

namespace Graph.Infrastructure.Utilities;

public class FileGrouper(IFileSystem fileSystem, IPictureAnalyzer pictureAnalyzer) {
    public async Task<List<FileGroup>> GroupFilesAsync(string sourcePath) {
        if (!fileSystem.Directory.Exists(sourcePath)) {
            return [];
        }

        var files = fileSystem.Directory.GetFiles(sourcePath);
        var groups = new Dictionary<string, FileGroup>();

        foreach (var filePath in files) {
            var fileName = fileSystem.Path.GetFileName(filePath);

            // Skip hidden/ghost files
            if (fileName.StartsWith('.') || fileName.StartsWith("._")) {
                continue;
            }

            var fileNameWithoutExtension = fileSystem.Path.GetFileNameWithoutExtension(filePath);

            if (!groups.TryGetValue(fileNameWithoutExtension, out var group)) {
                group = new FileGroup {
                    BaseName = fileNameWithoutExtension,
                    PrimaryDate = fileSystem.File.GetCreationTime(filePath) // Default if metadata fails
                };
                groups.Add(fileNameWithoutExtension, group);
            }

            group.FilePaths.Add(filePath);
        }

        // After grouping, try to find the best primary date for each group
        foreach (var group in groups.Values) {
            foreach (var filePath in group.FilePaths) {
                var timestampResult = await pictureAnalyzer.ExtractTimestamp(filePath);
                if (timestampResult is { IsError: false }) {
                    group.PrimaryDate = timestampResult.Value;
                    break; // Found a valid timestamp from metadata, move to next group
                }
            }
        }

        return groups.Values.ToList();
    }
}
