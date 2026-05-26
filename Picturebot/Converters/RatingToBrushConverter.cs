using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Picturebot.Converters;

public class RatingToBrushConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is int rating && parameter is string targetStr && int.TryParse(targetStr, out var targetRating)) {
            return rating >= targetRating ? Brushes.Gold : Brush.Parse("#444444");
        }
        return Brush.Parse("#444444");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
