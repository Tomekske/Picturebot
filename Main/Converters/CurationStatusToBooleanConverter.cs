using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Domain.Enums;

namespace Main.Converters;

public class CurationStatusToBooleanConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is CurationStatus status && parameter is string targetStr && Enum.TryParse<CurationStatus>(targetStr, out var targetStatus)) {
            return status == targetStatus;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
