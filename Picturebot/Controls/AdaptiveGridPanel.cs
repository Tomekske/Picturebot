using System;
using Avalonia;
using Avalonia.Controls;

namespace Picturebot.Controls;

/// <summary>
/// Grid panel that fills the available width: it fits as many columns as possible at
/// <see cref="MinItemWidth"/>, then stretches every item equally so rows end flush with the
/// right edge (no leftover gap like a WrapPanel with fixed-width items). Items in the last row
/// keep the same width and stay left-aligned.
/// </summary>
public class AdaptiveGridPanel : Panel {
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<AdaptiveGridPanel, double>(nameof(MinItemWidth), 280);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AdaptiveGridPanel, double>(nameof(Spacing), 16);

    static AdaptiveGridPanel() {
        AffectsMeasure<AdaptiveGridPanel>(MinItemWidthProperty, SpacingProperty);
    }

    /// <summary>Smallest width an item may get before a column is dropped.</summary>
    public double MinItemWidth {
        get => GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>Horizontal and vertical gap between items.</summary>
    public double Spacing {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private (int Columns, double ItemWidth) GetColumns(double availableWidth) {
        var spacing = Spacing;
        var minWidth = Math.Max(1, MinItemWidth);
        if (double.IsInfinity(availableWidth) || availableWidth <= 0) {
            return (1, minWidth);
        }

        var columns = Math.Max(1, (int)Math.Floor((availableWidth + spacing) / (minWidth + spacing)));
        var itemWidth = (availableWidth - spacing * (columns - 1)) / columns;
        return (columns, Math.Max(1, itemWidth));
    }

    protected override Size MeasureOverride(Size availableSize) {
        var (columns, itemWidth) = GetColumns(availableSize.Width);
        double totalHeight = 0, rowHeight = 0;
        var index = 0;

        foreach (var child in Children) {
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);

            if (++index % columns == 0) {
                totalHeight += rowHeight + Spacing;
                rowHeight = 0;
            }
        }

        totalHeight += rowHeight > 0 ? rowHeight : (index > 0 ? -Spacing : 0);
        var width = double.IsInfinity(availableSize.Width) ? itemWidth : availableSize.Width;
        return new Size(width, Math.Max(0, totalHeight));
    }

    protected override Size ArrangeOverride(Size finalSize) {
        var (columns, itemWidth) = GetColumns(finalSize.Width);
        double y = 0, rowHeight = 0;
        var index = 0;

        foreach (var child in Children) {
            var column = index % columns;
            if (column == 0 && index > 0) {
                y += rowHeight + Spacing;
                rowHeight = 0;
            }

            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(column * (itemWidth + Spacing), y, itemWidth, height));
            rowHeight = Math.Max(rowHeight, height);
            index++;
        }

        return finalSize;
    }
}
