using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Domain.Enums;

namespace Main.Converters;

public class CurationStatusToBrushConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is CurationStatus status && parameter is string targetStr && Enum.TryParse<CurationStatus>(targetStr, out var targetStatus)) {
            if (status == targetStatus) {
                return targetStatus switch {
                    CurationStatus.Flagged => Brushes.Orange,
                    CurationStatus.Unflagged => Brushes.SkyBlue,
                    CurationStatus.Rejected => Brushes.Red,
                    _ => Brushes.Gray
                };
            }
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
