using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Domain.Enums;

using Picturebot.Themes;

namespace Picturebot.Converters;

public class CurationStatusToBrushConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is CurationStatus status && parameter is string targetStr &&
            Enum.TryParse<CurationStatus>(targetStr, out var targetStatus)) {
            if (status == targetStatus) {
                return targetStatus switch {
                    CurationStatus.Flagged => ThemeBrushes.Get("Status.Picked"),
                    CurationStatus.Unflagged => ThemeBrushes.Get("Accent.Foreground"),
                    CurationStatus.Rejected => ThemeBrushes.Get("Status.Rejected"),
                    _ => ThemeBrushes.Get("Text.Muted")
                };
            }
        }

        return ThemeBrushes.Get("Text.Muted");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
