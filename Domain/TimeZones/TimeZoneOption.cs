namespace Domain.TimeZones;

/// <summary>
///     One pickable time zone, e.g. "United States — Denver · Mountain (most areas)".
/// </summary>
public class TimeZoneOption {
    /// <summary>
    ///     The IANA id that is stored, e.g. "America/Denver".
    /// </summary>
    public required string Id { get; init; }

    public required string CountryCode { get; init; }
    public required string CountryName { get; init; }

    /// <summary>
    ///     The city part of the id, e.g. "Denver" or "Ho Chi Minh".
    /// </summary>
    public required string City { get; init; }

    /// <summary>
    ///     The tzdb description for countries with more than one zone, e.g. "Mountain (most areas)".
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    ///     "UTC+8", or "UTC+1 / +2" for zones with summer time.
    /// </summary>
    public required string OffsetText { get; init; }

    /// <summary>
    ///     Lowercase text without accents that a search query is matched against.
    /// </summary>
    public required string SearchText { get; init; }

    public string Title => $"{CountryName} — {City}";
    public string Subtitle => Comment == null ? OffsetText : $"{Comment} · {OffsetText}";

    /// <summary>
    ///     What the picker's text box shows once this zone is chosen.
    /// </summary>
    public override string ToString() => $"{Title} ({OffsetText})";
}
