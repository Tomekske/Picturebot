using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Domain.Enums;

namespace Picturebot.Converters;

public class ColorLabelToStrokeThicknessConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is ColorLabel label && parameter is string targetStr &&
            Enum.TryParse<ColorLabel>(targetStr, out var targetLabel)) {
            return label == targetLabel ? 2 : 0;
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
