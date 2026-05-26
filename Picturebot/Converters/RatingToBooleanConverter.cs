using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Picturebot.Converters;

public class RatingToBooleanConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is int rating && parameter is string targetStr && int.TryParse(targetStr, out var targetRating)) {
            return rating >= targetRating;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
