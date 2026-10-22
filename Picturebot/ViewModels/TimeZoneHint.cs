using Domain.TimeZones;

namespace Picturebot.ViewModels;

/// <summary>
///     The text under a time zone picker that says where the zone comes from: set here, inherited from a folder
///     above, or the default from the settings.
/// </summary>
public static class TimeZoneHint {
    /// <summary>
    ///     The zone an album or folder uses when it has none of its own: the nearest folder's, else the default.
    /// </summary>
    public static string? InheritedZoneId(TimeZoneSource? inherited, string? configuredDefaultZoneId) {
        return inherited?.ZoneId ?? TimeZoneCatalog.DefaultZoneId(configuredDefaultZoneId);
    }

    /// <summary>
    ///     A zone equal to the inherited one is stored as "not set", so it keeps following the folder above.
    /// </summary>
    public static string? Normalize(string? ownZoneId, TimeZoneSource? inherited, string? configuredDefaultZoneId) {
        if (string.IsNullOrWhiteSpace(ownZoneId)) return null;
        return string.Equals(ownZoneId, InheritedZoneId(inherited, configuredDefaultZoneId), System.StringComparison.OrdinalIgnoreCase)
            ? null
            : ownZoneId;
    }

    public static string Describe(string? ownZoneId, TimeZoneSource? inherited, string? configuredDefaultZoneId, bool isFolder) {
        var inheritedZoneId = InheritedZoneId(inherited, configuredDefaultZoneId);
        var inheritedText = inherited != null
            ? $"{inherited.NodeName}'s time zone"
            : "the default time zone from Settings";

        if (string.IsNullOrWhiteSpace(ownZoneId) ||
            string.Equals(ownZoneId, inheritedZoneId, System.StringComparison.OrdinalIgnoreCase)) {
            return inherited != null
                ? $"Inherited from {inherited.NodeName}."
                : "Not set: uses the default time zone from Settings.";
        }

        var children = isFolder ? " Albums and folders inside use it unless they set their own." : string.Empty;
        return $"Set here.{children} Clear it to use {inheritedText}, {TimeZoneCatalog.Describe(inheritedZoneId)}.";
    }
}
