using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Domain.Entities;

/// <summary>
///     Represents a logical folder node used for organizing other nodes within the hierarchy.
/// </summary>
[Table("folders")]
public class Folder : Node
{
}