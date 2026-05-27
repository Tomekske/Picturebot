using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Domain.Enums;

namespace Picturebot.Converters;

public class ColorLabelToBrushConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is ColorLabel label) {
            return label switch {
                ColorLabel.Red => Brush.Parse("#CC3333"),
                ColorLabel.Orange => Brush.Parse("#E67E22"),
                ColorLabel.Yellow => Brush.Parse("#CCCC33"),
                ColorLabel.Green => Brush.Parse("#33CC33"),
                ColorLabel.Blue => Brush.Parse("#3333CC"),
                ColorLabel.Pink => Brush.Parse("#E91E63"),
                ColorLabel.Purple => Brush.Parse("#CC33CC"),
                _ => Brushes.Transparent
            };
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
