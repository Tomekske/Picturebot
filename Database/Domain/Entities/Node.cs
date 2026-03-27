using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Domain.Enums;

namespace Database.Domain.Entities;

/// <summary>
///     Represents a base element in the organizational hierarchy, supporting recursive relationships.
/// </summary>
[Table("nodes")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Album), "album")]
[JsonDerivedType(typeof(Folder), "folder")]
[JsonDerivedType(typeof(Picture), "picture")]
[JsonDerivedType(typeof(Node), "node")] // Fallback if base is used
public class Node {
    /// <summary>
    ///     The unique identifier for the node.
    /// </summary>ghghj
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     The identifier of the parent node, or null if this is a root node.
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    ///     Navigation property for the recursive parent-child relationship.
    /// </summary>
    [JsonIgnore]
    [ForeignKey(nameof(ParentId))]
    public Node? Parent { get; set; }

    /// <summary>
    ///     The classification of the node (e.g., Folder, Album, Picture).
    /// </summary>
    [Required]
    [JsonIgnore]
    public NodeType Type { get; set; }

    /// <summary>
    ///     The display name of the node.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     A collection of immediate child nodes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<Node>? Children { get; set; } = new List<Node>();
}
