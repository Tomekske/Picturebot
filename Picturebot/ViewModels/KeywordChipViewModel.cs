using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Picturebot.ViewModels;

public partial class KeywordChipViewModel : ObservableObject {
    public string RawValue { get; }
    public string DisplayText { get; }
    public bool IsHierarchical { get; }
    public string? ParentPath { get; }
    public string LeafName { get; }
    public string Tooltip { get; }

    public KeywordChipViewModel(string rawValue, string displayText, bool isHierarchical, string? parentPath, string leafName, string tooltip) {
        RawValue = rawValue;
        DisplayText = displayText;
        IsHierarchical = isHierarchical;
        ParentPath = parentPath;
        LeafName = leafName;
        Tooltip = tooltip;
    }

    public static KeywordChipViewModel FromHierarchicalPath(string rawPath) {
        var normalized = NormalizePath(rawPath);
        var parts = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length <= 1) {
            var leaf = parts.Length == 1 ? parts[0] : normalized;
            return new KeywordChipViewModel(normalized, leaf, false, null, leaf, $"Tag: {leaf}");
        }

        var parentParts = parts.Take(parts.Length - 1).ToArray();
        var parentPath = string.Join(" › ", parentParts);
        var leafName = parts.Last();
        var displayText = $"{parentPath} › {leafName}";

        return new KeywordChipViewModel(
            normalized,
            displayText,
            isHierarchical: true,
            parentPath: parentPath,
            leafName: leafName,
            tooltip: $"Hierarchical Tag: {displayText}"
        );
    }

    public static KeywordChipViewModel FromFlatTag(string tagName) {
        var trimmed = tagName.Trim();
        return new KeywordChipViewModel(
            trimmed,
            trimmed,
            isHierarchical: false,
            parentPath: null,
            leafName: trimmed,
            tooltip: $"Tag: {trimmed}"
        );
    }

    public static string NormalizePath(string rawPath) {
        if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;
        return rawPath
            .Replace(" › ", "|")
            .Replace(" > ", "|")
            .Replace("›", "|")
            .Replace(">", "|")
            .Replace('/', '|')
            .Replace('\\', '|')
            .Trim('|')
            .Trim();
    }
}
