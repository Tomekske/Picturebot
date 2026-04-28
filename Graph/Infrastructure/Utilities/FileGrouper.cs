using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Graph.Domain.Models;

namespace Graph.Infrastructure.Utilities;

public class FileGrouper(IFileSystem fileSystem) {
    private readonly TimeSpan _burstTimeThreshold = TimeSpan.FromSeconds(2);
    private readonly int _pHashSimilarityThreshold = 10;

    // You would pass in your cached data from your repository/database here
    public List<FileGroup> GroupFiles(List<CachedPictureData> cachedFiles) {
        if (cachedFiles == null || cachedFiles.Count == 0) {
            return [];
        }

        var finalGroups = new List<FileGroup>();
        var ungroupedFiles = new List<CachedPictureData>(cachedFiles);

        // PASS 1: Explicit Pattern Matching (Fastest)
        var patternGroups = GroupByNamePattern(ungroupedFiles);
        finalGroups.AddRange(patternGroups);

        // Remove pattern-grouped files from the pool
        var groupedPaths = patternGroups.SelectMany(g => g.FilePaths).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ungroupedFiles.RemoveAll(f => groupedPaths.Contains(f.FilePath));

        // PASS 2: Time & Hash Sliding Window (Now O(N) because data is cached)
        var timeAndHashGroups = GroupByTimeAndHash(ungroupedFiles);
        finalGroups.AddRange(timeAndHashGroups);

        return finalGroups;
    }

    private List<FileGroup> GroupByNamePattern(List<CachedPictureData> files) {
        var groups = new Dictionary<string, FileGroup>(StringComparer.OrdinalIgnoreCase);
        var burstPattern = new Regex(@"^(.*?)(?:_BURST\d+|-\d+)(?=\.[^.]+$)", RegexOptions.IgnoreCase);

        foreach (var file in files) {
            var fileName = fileSystem.Path.GetFileName(file.FilePath);
            var match = burstPattern.Match(fileName);

            var groupKey = match.Success
                ? match.Groups[1].Value
                : fileSystem.Path.GetFileNameWithoutExtension(file.FilePath);

            if (!groups.TryGetValue(groupKey, out var group)) {
                group = new FileGroup {
                    BaseName = groupKey,
                    PrimaryDate = file.PrimaryDate // Inherit date from cache
                };
                groups.Add(groupKey, group);
            }

            group.FilePaths.Add(file.FilePath);
        }

        return groups.Values.Where(g => g.FilePaths.Count > 1).ToList();
    }

    private List<FileGroup> GroupByTimeAndHash(List<CachedPictureData> files) {
        var finalGroups = new List<FileGroup>();
        if (!files.Any()) {
            return finalGroups;
        }

        // Sort chronologically using the cached dates
        var sortedFiles = files.OrderBy(f => f.PrimaryDate).ToList();

        var currentGroup = new FileGroup {
            BaseName = "Burst_" + sortedFiles[0].PrimaryDate.ToString("yyyyMMdd_HHmmss"),
            PrimaryDate = sortedFiles[0].PrimaryDate
        };
        currentGroup.FilePaths.Add(sortedFiles[0].FilePath);

        var previousFile = sortedFiles[0];

        for (var i = 1; i < sortedFiles.Count; i++) {
            var currentFile = sortedFiles[i];
            var timeDifference = currentFile.PrimaryDate - previousFile.PrimaryDate;

            // Since Hash is cached, we evaluate Time and Hash simultaneously
            if (timeDifference <= _burstTimeThreshold) {
                var hammingDistance = CalculateHammingDistance(previousFile.PHash, currentFile.PHash);

                if (hammingDistance <= _pHashSimilarityThreshold) {
                    currentGroup.FilePaths.Add(currentFile.FilePath);
                    previousFile = currentFile;
                    continue; // Move to next file in the burst
                }
            }

            // If we reach here, the burst is broken. Save the old group and start a new one.
            finalGroups.Add(currentGroup);

            currentGroup = new FileGroup {
                BaseName = "Burst_" + currentFile.PrimaryDate.ToString("yyyyMMdd_HHmmss"),
                PrimaryDate = currentFile.PrimaryDate
            };
            currentGroup.FilePaths.Add(currentFile.FilePath);
            previousFile = currentFile;
        }

        // Add the trailing group
        finalGroups.Add(currentGroup);

        return finalGroups;
    }

    private int CalculateHammingDistance(ulong hash1, ulong hash2) {
        var xor = hash1 ^ hash2;
        var distance = 0;
        while (xor != 0) {
            distance += 1;
            xor &= xor - 1;
        }

        return distance;
    }
}

// DTO representing what you pull from your database/cache BEFORE grouping
public class CachedPictureData {
    public string FilePath { get; set; } = string.Empty;
    public DateTime PrimaryDate { get; set; }
    public ulong PHash { get; set; }
}
