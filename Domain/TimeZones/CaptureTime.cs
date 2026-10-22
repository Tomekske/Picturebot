namespace Domain.TimeZones;

/// <summary>
///     A capture time as shown to the user: the wall-clock time where the picture was taken.
/// </summary>
/// <param name="Local">The time in the album's zone.</param>
/// <param name="ZoneId">The zone it is shown in, or null when it couldn't be converted (camera time is shown).</param>
/// <param name="UtcOffset">The zone's offset at that moment, summer time included.</param>
public readonly record struct LocalCaptureTime(DateTime Local, string? ZoneId, TimeSpan? UtcOffset);

/// <summary>
///     Converts the camera's clock time to the time where the picture was taken. Nothing is stored: the conversion
///     is done every time pictures are shown, so changing an album's zone never touches any file.
/// </summary>
public static class CaptureTime {
    /// <summary>
    ///     camera time → UTC → the target zone. The camera's offset comes from EXIF (OffsetTimeOriginal) when the
    ///     camera wrote one; otherwise the camera clock is assumed to run on <paramref name="cameraZoneId" />,
    ///     summer time included. Summer time in the target zone is applied for the picture's own date.
    /// </summary>
    public static LocalCaptureTime ToLocal(DateTime cameraTime, TimeSpan? cameraUtcOffset, string? cameraZoneId,
        string? targetZoneId) {
        var unconverted = new LocalCaptureTime(cameraTime, null, cameraUtcOffset);
        if (!TryFindZone(targetZoneId, out var target)) return unconverted;

        DateTime utc;
        if (cameraUtcOffset is { } offset) {
            utc = DateTime.SpecifyKind(cameraTime - offset, DateTimeKind.Utc);
        } else if (TryFindZone(cameraZoneId, out var cameraZone)) {
            utc = ToUtc(DateTime.SpecifyKind(cameraTime, DateTimeKind.Unspecified), cameraZone);
        } else {
            return unconverted;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, target);
        return new LocalCaptureTime(local, target.Id, target.GetUtcOffset(utc));
    }

    private static DateTime ToUtc(DateTime clockTime, TimeZoneInfo zone) {
        // In the hour skipped when summer time starts the camera clock hadn't moved forward yet: read it an hour later.
        // In the hour that happens twice when it ends, .NET picks standard time.
        if (zone.IsInvalidTime(clockTime)) {
            clockTime = clockTime.AddHours(1);
        }
        return TimeZoneInfo.ConvertTimeToUtc(clockTime, zone);
    }

    private static bool TryFindZone(string? id, out TimeZoneInfo zone) {
        zone = null!;
        return !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out zone!);
    }

    /// <summary>
    ///     "+01:00" as written in EXIF and in the XMP.
    /// </summary>
    public static string FormatOffset(TimeSpan offset) {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"{sign}{offset.Duration():hh\\:mm}";
    }

    /// <summary>
    ///     Parses "+01:00" / "-04:30" (EXIF OffsetTimeOriginal); null when the text isn't an offset.
    /// </summary>
    public static TimeSpan? ParseOffset(string? text) {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 2 || (text[0] != '+' && text[0] != '-')) return null;
        if (!TimeSpan.TryParseExact(text[1..], "hh\\:mm", null, out var value)) return null;
        if (value > TimeSpan.FromHours(14)) return null;
        return text[0] == '-' ? -value : value;
    }
}
