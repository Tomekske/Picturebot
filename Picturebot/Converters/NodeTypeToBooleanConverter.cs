using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Domain.Enums;

namespace Picturebot.Converters;

public class NodeTypeToBooleanConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is NodeType nodeType && parameter is string target) {
            return nodeType.ToString().Equals(target, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
