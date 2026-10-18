using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Domain.Enums;

using Picturebot.Themes;

namespace Picturebot.Converters;

public class ColorLabelToBrushConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is ColorLabel label) {
            return ThemeBrushes.ForLabel(label);
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
