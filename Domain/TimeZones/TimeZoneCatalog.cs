using System.Globalization;
using System.Text;

namespace Domain.TimeZones;

/// <summary>
///     The time zones a user can pick, one entry per country and zone, read from the tzdb tables
///     zone.tab and iso3166.tab (embedded, public domain). Countries with several zones (United States, Russia)
///     get one entry per zone; overseas territories (Réunion, Guam) are countries of their own in these tables.
/// </summary>
public static class TimeZoneCatalog {
    private static readonly Lazy<IReadOnlyList<TimeZoneOption>> Options = new(Load);

    public static IReadOnlyList<TimeZoneOption> All => Options.Value;

    public static TimeZoneOption? Find(string? id) {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return All.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Every word of the query must appear in the country, city, zone id or description; accents and case
    ///     are ignored. Matches on the start of the country or city come first.
    /// </summary>
    public static IEnumerable<TimeZoneOption> Search(string? query) {
        var tokens = Tokenize(query);
        if (tokens.Length == 0) return All;

        var first = tokens[0];
        return All
            .Where(o => Matches(o, tokens))
            .OrderBy(o => Normalize(o.CountryName).StartsWith(first, StringComparison.Ordinal) ||
                          Normalize(o.City).StartsWith(first, StringComparison.Ordinal) ? 0 : 1);
    }

    public static bool Matches(TimeZoneOption option, string? query) => Matches(option, Tokenize(query));

    private static bool Matches(TimeZoneOption option, string[] tokens) {
        return tokens.All(t => option.SearchText.Contains(t, StringComparison.Ordinal));
    }

    private static string[] Tokenize(string? query) {
        return Normalize(query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    ///     The zone used for albums without one: the zone from the settings, or this computer's zone when none is set.
    /// </summary>
    public static string? DefaultZoneId(string? configuredZoneId) {
        return string.IsNullOrWhiteSpace(configuredZoneId) ? SystemZoneId() : configuredZoneId;
    }

    /// <summary>
    ///     "Belgium — Brussels (UTC+1 / +2)" for a known zone, the raw id otherwise.
    /// </summary>
    public static string Describe(string? zoneId) {
        return Find(zoneId)?.ToString() ?? zoneId ?? "unknown";
    }

    /// <summary>
    ///     The IANA id of this computer's time zone, e.g. "Europe/Brussels"; null when it has no IANA equivalent.
    /// </summary>
    public static string? SystemZoneId() {
        var local = TimeZoneInfo.Local;
        if (local.HasIanaId) return local.Id;

        string? region = null;
        try {
            region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
        } catch (ArgumentException) {
            // Invariant culture: fall back to the zone's default region.
        }

        if (region != null && TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, region, out var regional)) {
            return regional;
        }
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var iana) ? iana : null;
    }

    /// <summary>
    ///     "UTC+8" for a zone without summer time, "UTC+1 / +2" for one with it, based on the given year.
    /// </summary>
    public static string FormatOffsets(TimeZoneInfo zone, int year) {
        var offsets = Enumerable.Range(1, 12)
            .Select(month => zone.GetUtcOffset(new DateTimeOffset(year, month, 1, 12, 0, 0, TimeSpan.Zero)))
            .Distinct()
            .Order()
            .ToList();
        return "UTC" + string.Join(" / ", offsets.Select(FormatOffset));
    }

    private static string FormatOffset(TimeSpan offset) {
        var sign = offset < TimeSpan.Zero ? "−" : "+";
        var abs = offset.Duration();
        return abs.Minutes == 0 ? $"{sign}{abs.Hours}" : $"{sign}{abs.Hours}:{abs.Minutes:00}";
    }

    private static List<TimeZoneOption> Parse(string zoneTab, string iso3166Tab, int year) {
        var countries = ReadRows(iso3166Tab)
            .Where(r => r.Length >= 2)
            .ToDictionary(r => r[0], r => r[1], StringComparer.Ordinal);

        var rows = ReadRows(zoneTab).Where(r => r.Length >= 3).ToList();
        var zonesPerCountry = rows.GroupBy(r => r[0]).ToDictionary(g => g.Key, g => g.Count());

        var options = new List<TimeZoneOption>();
        foreach (var row in rows) {
            var (code, id) = (row[0], row[2]);
            // Zones this machine can't resolve can't be converted either, so they're not offered.
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) continue;

            var country = countries.GetValueOrDefault(code, code);
            var city = id[(id.LastIndexOf('/') + 1)..].Replace('_', ' ');
            var comment = row.Length >= 4 && zonesPerCountry[code] > 1 ? row[3] : null;

            options.Add(new TimeZoneOption {
                Id = id,
                CountryCode = code,
                CountryName = country,
                City = city,
                Comment = comment,
                OffsetText = FormatOffsets(zone, year),
                SearchText = Normalize($"{country} {code} {id.Replace('/', ' ').Replace('_', ' ')} {comment}")
            });
        }

        // zone.tab lists a country's zones in a meaningful order (e.g. US east to west), so only sort by country.
        return options
            .Select((option, index) => (option, index))
            .OrderBy(x => x.option.CountryName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.index)
            .Select(x => x.option)
            .ToList();
    }

    private static IEnumerable<string[]> ReadRows(string table) {
        return table.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'));
    }

    private static string Normalize(string text) {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed) {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) {
                builder.Append(c);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyList<TimeZoneOption> Load() {
        return Parse(ReadResource("zone.tab"), ReadResource("iso3166.tab"), DateTime.Today.Year);
    }

    private static string ReadResource(string name) {
        var assembly = typeof(TimeZoneCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream($"Domain.TimeZones.{name}")
                           ?? throw new InvalidOperationException($"Embedded time zone table {name} is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
