using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database.Domain.Entities;

/// <summary>
///     Represents an album node which acts as a container for pictures and maintains a physical directory mapping.
/// </summary>
[Table("albums")]
public class Album : Node {
    /// <summary>
    ///     A unique identifier used to map the album to its corresponding directory in the physical library.
    /// </summary>
    [MaxLength(36)]
    public string? Uuid { get; set; }

    /// <summary>
    ///     The IANA time zone (e.g. "Asia/Taipei") the album's pictures were taken in.
    ///     Null means the default time zone from the settings.
    /// </summary>
    [MaxLength(64)]
    public string? TimeZoneId { get; set; }
}
