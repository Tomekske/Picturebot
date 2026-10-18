using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Picturebot.Converters;

/// <summary>
/// True when the bound enum (or int) value equals the ConverterParameter, compared by name.
/// A null value (e.g. a "mixed" multi-selection) is never equal. Used for active-state styling.
/// </summary>
public class EnumEqualsConverter : IValueConverter {
    public static readonly EnumEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        return value != null && parameter != null &&
               string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }
}
