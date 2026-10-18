using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Serilog;

namespace Picturebot.Services;

public interface IRecentLocationsService {
    /// <summary>
    ///     Folder ids most recently used as a parent in the New dialog, newest first.
    /// </summary>
    IReadOnlyList<int> GetRecent();

    void Remember(int folderId);

    /// <summary>
    ///     The disk folder last picked as an album source, or null. Survives restarts.
    /// </summary>
    string? LastSourceFolder { get; set; }
}

/// <summary>
///     Keeps the last few parent folders used in the New dialog in a small JSON file next to the database, and the last
///     source folder picked for an import in a text file beside it.
/// </summary>
public class RecentLocationsService : IRecentLocationsService {
    private const int MaxEntries = 5;
    private readonly string _filePath;
    private readonly string _sourceFolderPath;
    private List<int>? _cache;

    public RecentLocationsService(string appDirectory) {
        _filePath = Path.Combine(appDirectory, "recent-locations.json");
        _sourceFolderPath = Path.Combine(appDirectory, "last-source-folder.txt");
    }

    public string? LastSourceFolder {
        get {
            try {
                if (File.Exists(_sourceFolderPath)) {
                    var path = File.ReadAllText(_sourceFolderPath).Trim();
                    return path.Length > 0 ? path : null;
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Could not read the last source folder from {Path}", _sourceFolderPath);
            }

            return null;
        }
        set {
            try {
                File.WriteAllText(_sourceFolderPath, value ?? string.Empty);
            } catch (Exception ex) {
                Log.Warning(ex, "Could not save the last source folder to {Path}", _sourceFolderPath);
            }
        }
    }

    public IReadOnlyList<int> GetRecent() {
        return Load();
    }

    public void Remember(int folderId) {
        var list = Load();
        list.Remove(folderId);
        list.Insert(0, folderId);
        if (list.Count > MaxEntries) {
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        }

        try {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(list));
        } catch (Exception ex) {
            Log.Warning(ex, "Could not save recent locations to {Path}", _filePath);
        }
    }

    private List<int> Load() {
        if (_cache != null) return _cache;

        try {
            if (File.Exists(_filePath)) {
                _cache = JsonSerializer.Deserialize<List<int>>(File.ReadAllText(_filePath))?.Distinct().ToList();
            }
        } catch (Exception ex) {
            Log.Warning(ex, "Could not read recent locations from {Path}", _filePath);
        }

        return _cache ??= new List<int>();
    }
}
