using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Domain.Enums;

namespace Picturebot.Themes;

/// <summary>
/// Resolves design tokens from Themes/Tokens.axaml for code (converters, view models)
/// that cannot use {DynamicResource}. Never hard-code colors in C#; add a token instead.
/// </summary>
public static class ThemeBrushes {
    public static IBrush Get(string key) {
        if (Application.Current?.TryGetResource(key, ThemeVariant.Dark, out var resource) == true &&
            resource is IBrush brush) {
            return brush;
        }

        return Brushes.Transparent;
    }

    public static IBrush ForLabel(ColorLabel label) {
        return label switch {
            ColorLabel.Red => Get("Label.Red"),
            ColorLabel.Orange => Get("Label.Orange"),
            ColorLabel.Yellow => Get("Label.Yellow"),
            ColorLabel.Green => Get("Label.Green"),
            ColorLabel.Blue => Get("Label.Blue"),
            ColorLabel.Pink => Get("Label.Pink"),
            ColorLabel.Purple => Get("Label.Purple"),
            _ => Brushes.Transparent
        };
    }
}
