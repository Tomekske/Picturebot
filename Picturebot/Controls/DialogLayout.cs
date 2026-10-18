using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Picturebot.Controls;

/// <summary>Standard dialog widths; values live in Themes/Tokens.axaml (Dialog.Width.*).</summary>
public enum DialogSize {
    /// <summary>440px: confirmations and small info dialogs.</summary>
    Small,
    /// <summary>560px: forms (create/edit/import).</summary>
    Medium,
    /// <summary>860px: rich content (settings, AI curation).</summary>
    Large
}

/// <summary>
/// Standard dialog layout matching the Settings dialog: a header with title, optional icon
/// and close button, a padded body (Content) and a footer for the action buttons.
/// Styled by the ControlTheme in Themes/ControlThemes.axaml.
/// </summary>
public class DialogLayout : ContentControl {
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DialogLayout, string?>(nameof(Title));

    public static readonly StyledProperty<object?> HeaderIconProperty =
        AvaloniaProperty.Register<DialogLayout, object?>(nameof(HeaderIcon));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<DialogLayout, object?>(nameof(Footer));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<DialogLayout, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<DialogSize> SizeProperty =
        AvaloniaProperty.Register<DialogLayout, DialogSize>(nameof(Size), DialogSize.Medium);

    /// <summary>Fraction of the window height a dialog may use before its body scrolls.</summary>
    private const double MaxWindowHeightRatio = 0.85;

    private TopLevel? _topLevel;

    public string? Title {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Optional icon shown before the title (e.g. a warning icon).</summary>
    public object? HeaderIcon {
        get => GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    /// <summary>Footer content, usually a StackPanel with Secondary and Primary buttons.</summary>
    public object? Footer {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>Command for the header close (✕) button; the button is hidden when null.</summary>
    public ICommand? CloseCommand {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>Width tier of the dialog. Never set Width on a dialog directly.</summary>
    public DialogSize Size {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel != null) {
            _topLevel.SizeChanged += OnTopLevelSizeChanged;
            UpdateMaxHeight();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        if (_topLevel != null) {
            _topLevel.SizeChanged -= OnTopLevelSizeChanged;
            _topLevel = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateMaxHeight();

    // Cap the dialog at a share of the window height; the body ScrollViewer then scrolls
    // while the header and footer (with the action buttons) stay visible.
    private void UpdateMaxHeight() {
        if (_topLevel == null) return;
        var available = _topLevel.ClientSize.Height;
        MaxHeight = available > 0 ? Math.Floor(available * MaxWindowHeightRatio) : double.PositiveInfinity;
    }
}
