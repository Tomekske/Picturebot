using System.Collections.Generic;

namespace Domain.Models;

/// <summary>
///     Represents the result of an AI vision curation evaluation on a group of pictures.
/// </summary>
public class AiCurationResult {
    /// <summary>
    ///     The identifier of the single chosen best image in the burst/group.
    /// </summary>
    public string BestPickId { get; set; } = string.Empty;

    /// <summary>
    ///     Concise, objective, diagnostic observations keyed by picture identifier.
    /// </summary>
    public Dictionary<string, string> Feedback { get; set; } = new();

    /// <summary>
    ///     The raw response text from the AI model (for debugging and logging).
    /// </summary>
    public string? RawResponse { get; set; }
}
