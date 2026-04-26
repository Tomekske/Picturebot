using System;
using Avalonia;
using Avalonia.Controls;

namespace Picturebot.Controls;

/// <summary>
/// A specialized panel that enforces a specific aspect ratio (Height = Width * Ratio)
/// during the Avalonia layout pass.
/// </summary>
public class AspectRatioPanel : Panel {
    /// <summary>
    /// Defines the Ratio property (Height = Width * Ratio). Default is 0.75 (4:3).
    /// </summary>
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<AspectRatioPanel, double>(nameof(Ratio), 0.75);

    public double Ratio {
        get => GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) {
        // Fallback to 200 if width is infinite or 0 during initial pass
        double width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0 
            ? 200 
            : availableSize.Width;

        double height = width * Ratio;
        Size constraint = new Size(width, height);

        foreach (var child in Children) {
            child.Measure(constraint);
        }

        return constraint;
    }

    protected override Size ArrangeOverride(Size finalSize) {
        Rect finalRect = new Rect(finalSize);

        foreach (var child in Children) {
            child.Arrange(finalRect);
        }

        return finalSize;
    }
}
