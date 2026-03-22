using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Domain.Entities;

/// <summary>
///     Represents an album node which acts as a container for pictures and maintains a physical directory mapping.
/// </summary>
[Table("albums")]
public class Album : Node
{
    /// <summary>
    ///     A unique identifier used to map the album to its corresponding directory in the physical library.
    /// </summary>
    [MaxLength(36)]
    [Column("uuid")]
    public string? Uuid { get; set; }
}
