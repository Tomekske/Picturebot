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
}

/// <summary>
///     Keeps the last few parent folders used in the New dialog in a small JSON file next to the database.
/// </summary>
public class RecentLocationsService : IRecentLocationsService {
    private const int MaxEntries = 5;
    private readonly string _filePath;
    private List<int>? _cache;

    public RecentLocationsService(string appDirectory) {
        _filePath = Path.Combine(appDirectory, "recent-locations.json");
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
