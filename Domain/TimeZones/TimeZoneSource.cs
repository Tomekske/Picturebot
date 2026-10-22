namespace Domain.TimeZones;

/// <summary>
///     Where an album's or folder's time zone comes from: the node itself or the nearest folder above it.
/// </summary>
/// <param name="ZoneId">The IANA zone, e.g. "America/New_York".</param>
/// <param name="NodeId">The album or folder that has the zone set.</param>
/// <param name="NodeName">That node's name, e.g. "Miami", for "Inherited from Miami".</param>
public record TimeZoneSource(string ZoneId, int NodeId, string NodeName);
