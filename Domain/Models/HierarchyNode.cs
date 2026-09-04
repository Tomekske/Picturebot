using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Domain.Models;

/// <summary>
///     Represents a node in the XMP hierarchical taxonomy tree.
/// </summary>
public class HierarchyNode {
    public Guid NodeId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Optional link to a canonical master tag. Null if this node is purely a category/folder node.
    /// </summary>
    public Guid? TagId { get; set; }

    public ObservableCollection<HierarchyNode> Children { get; set; } = new();

    /// <summary>
    ///     Computes the XMP hierarchical path given a parent prefix (e.g., "Seasons|Summer").
    /// </summary>
    public string GetXmpHierarchicalPath(string parentPath = "") {
        if (string.IsNullOrWhiteSpace(parentPath)) {
            return Name;
        }
        return $"{parentPath}|{Name}";
    }
}
