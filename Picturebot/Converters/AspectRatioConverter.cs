using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Picturebot.Converters;

public class AspectRatioConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is double width && double.TryParse(parameter?.ToString(), out var ratio)) {
            return width * ratio;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
