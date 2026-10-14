using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

using Picturebot.Themes;

namespace Picturebot.Converters;

public class RatingToBrushConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is int rating && parameter is string targetStr && int.TryParse(targetStr, out var targetRating)) {
            return ThemeBrushes.Get(rating >= targetRating ? "Status.Rating" : "Border.Strong");
        }
        return ThemeBrushes.Get("Border.Strong");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
