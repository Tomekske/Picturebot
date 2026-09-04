using System;

namespace Domain.Models;

/// <summary>
///     Represents a canonical master tag in the global tag pool.
/// </summary>
public class Tag {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
