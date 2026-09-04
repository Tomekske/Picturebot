using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.Interfaces;

namespace Graph.Infrastructure.Services;

public class TaxonomyService : ITaxonomyService {
    private readonly ISettingsService _settingsService;

    public TaxonomyService(ISettingsService settingsService) {
        _settingsService = settingsService;
    }

    public List<string> GetAncestorChain(string leafTagName) {
        if (string.IsNullOrWhiteSpace(leafTagName)) {
            return new List<string>();
        }

        var trimmedLeaf = leafTagName.Trim();
        var hierarchy = _settingsService.Current?.HierarchyNodes ?? new List<HierarchyNode>();
        var masterTags = _settingsService.Current?.MasterTags ?? new List<Tag>();

        // Find linked tag ID if available
        var matchingTag = masterTags.FirstOrDefault(t => t.Name.Equals(trimmedLeaf, StringComparison.OrdinalIgnoreCase));
        var pathNodes = new List<string>();

        if (FindPathToNode(hierarchy, trimmedLeaf, matchingTag?.Id, pathNodes)) {
            // Path includes all ancestors and the target node itself
            if (pathNodes.Count > 1) {
                return pathNodes.Take(pathNodes.Count - 1).ToList();
            }
        }

        return new List<string>();
    }

    public string GetFullHierarchicalPath(string leafTagName) {
        if (string.IsNullOrWhiteSpace(leafTagName)) {
            return string.Empty;
        }

        var trimmedLeaf = leafTagName.Trim();
        var ancestors = GetAncestorChain(trimmedLeaf);

        if (ancestors.Count == 0) {
            return trimmedLeaf;
        }

        return string.Join("|", ancestors) + "|" + trimmedLeaf;
    }

    public HashSet<string> ResolveTaxonomySubjectChain(string leafTagName) {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(leafTagName)) {
            return set;
        }

        var trimmedLeaf = leafTagName.Trim();
        var ancestors = GetAncestorChain(trimmedLeaf);

        foreach (var ancestor in ancestors) {
            if (!string.IsNullOrWhiteSpace(ancestor)) {
                set.Add(ancestor.Trim());
            }
        }
        set.Add(trimmedLeaf);

        return set;
    }

    private static bool FindPathToNode(IEnumerable<HierarchyNode> nodes, string targetName, Guid? targetTagId, List<string> currentPath) {
        foreach (var node in nodes) {
            currentPath.Add(node.Name);

            bool isMatch = node.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                           (targetTagId.HasValue && node.TagId == targetTagId.Value);

            if (isMatch) {
                return true;
            }

            if (node.Children != null && node.Children.Count > 0) {
                if (FindPathToNode(node.Children, targetName, targetTagId, currentPath)) {
                    return true;
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
        }

        return false;
    }
}
