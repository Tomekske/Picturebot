using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Picturebot.Converters;

public class RatingToBooleanConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is int rating && parameter is string paramStr) {
            bool negate = paramStr.StartsWith("!");
            string targetStr = negate ? paramStr.Substring(1) : paramStr;

            if (int.TryParse(targetStr, out var targetRating)) {
                bool result = rating >= targetRating;
                return negate ? !result : result;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
