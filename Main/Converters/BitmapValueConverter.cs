using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace Main.Converters;

public class BitmapValueConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is string path && !string.IsNullOrEmpty(path)) {
            try {
                return new Bitmap(path);
            } catch (Exception ex) {
                Log.Error(ex, "Failed to load bitmap from path: {Path}", path);
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
