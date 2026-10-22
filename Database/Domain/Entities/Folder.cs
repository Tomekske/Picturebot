using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Domain.Entities;

/// <summary>
///     Represents a logical folder node used for organizing other nodes within the hierarchy.
/// </summary>
[Table("folders")]
public class Folder : Node {
    /// <summary>
    ///     The IANA time zone (e.g. "Asia/Tokyo") suggested for new albums and folders created inside this folder.
    /// </summary>
    [MaxLength(64)]
    public string? TimeZoneId { get; set; }
}
