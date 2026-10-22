using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Domain.TimeZones;

namespace Picturebot.Controls;

/// <summary>
///     Searchable time zone dropdown: type a country, city or zone ("taiw", "denver", "réunion") and pick an entry
///     with the mouse or arrow keys + Enter. Binds the IANA id through <see cref="SelectedZoneId" />; with
///     <see cref="AllowNone" /> the zone can be cleared.
/// </summary>
public partial class TimeZonePicker : UserControl {
    public static readonly StyledProperty<string?> SelectedZoneIdProperty =
        AvaloniaProperty.Register<TimeZonePicker, string?>(nameof(SelectedZoneId),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> AllowNoneProperty =
        AvaloniaProperty.Register<TimeZonePicker, bool>(nameof(AllowNone));

    /// <summary>
    ///     The zone used when none is chosen (from a folder above, or the default). Shown greyed out in the empty box.
    /// </summary>
    public static readonly StyledProperty<string?> InheritedZoneIdProperty =
        AvaloniaProperty.Register<TimeZonePicker, string?>(nameof(InheritedZoneId));

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<TimeZonePicker, string?>(nameof(Watermark), "Search a country or city…");

    private const int MaxResults = 150;

    // Set while the text box shows the chosen zone, so that text change isn't treated as a search.
    private bool _showingSelection;

    public TimeZonePicker() {
        InitializeComponent();

        SearchBox.TextChanged += SearchBox_TextChanged;
        SearchBox.GotFocus += SearchBox_GotFocus;
        SearchBox.LostFocus += (_, _) => CloseWhenFocusLeft();
        // Tunnel, so the arrow keys and Enter reach the list before the text box uses them.
        SearchBox.AddHandler(KeyDownEvent, SearchBox_KeyDown, RoutingStrategies.Tunnel);

        Results.Tapped += Results_Tapped;
        Results.LostFocus += (_, _) => CloseWhenFocusLeft();
        // Focus may have left while the mouse was over the list; close once it moves off
        DropDownBorder.PointerExited += (_, _) => CloseWhenFocusLeft();
        ClearButton.Click += (_, _) => SetCurrentValue(SelectedZoneIdProperty, null);
        ClearButton.IsVisible = false;
    }

    public string? SelectedZoneId {
        get => GetValue(SelectedZoneIdProperty);
        set => SetValue(SelectedZoneIdProperty, value);
    }

    public bool AllowNone {
        get => GetValue(AllowNoneProperty);
        set => SetValue(AllowNoneProperty, value);
    }

    public string? Watermark {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public string? InheritedZoneId {
        get => GetValue(InheritedZoneIdProperty);
        set => SetValue(InheritedZoneIdProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedZoneIdProperty || change.Property == AllowNoneProperty ||
            change.Property == InheritedZoneIdProperty || change.Property == WatermarkProperty) {
            ShowSelectedZone();
        }
    }

    /// <summary>
    ///     Puts the chosen zone back in the text box; an id the catalog doesn't list is shown as is.
    /// </summary>
    private void ShowSelectedZone() {
        _showingSelection = true;
        try {
            SearchBox.Text = string.IsNullOrEmpty(SelectedZoneId) ? string.Empty : TimeZoneCatalog.Describe(SelectedZoneId);
        } finally {
            _showingSelection = false;
        }
        ClearButton.IsVisible = AllowNone && !string.IsNullOrEmpty(SelectedZoneId);
        // Nothing chosen: the greyed-out placeholder shows the zone that applies instead
        SearchBox.Watermark = AllowNone && !string.IsNullOrEmpty(InheritedZoneId)
            ? TimeZoneCatalog.Describe(InheritedZoneId)
            : Watermark;
    }

    private void Commit(TimeZoneOption? option) {
        DropDown.IsOpen = false;
        if (option != null) {
            SetCurrentValue(SelectedZoneIdProperty, option.Id);
        }
        // Also when the id didn't change: the box may still hold a half-typed search.
        ShowSelectedZone();
    }

    private void OpenResults(string? query) {
        var results = TimeZoneCatalog.Search(query).Take(MaxResults).ToList();
        Results.ItemsSource = results;
        var current = string.IsNullOrEmpty(SelectedZoneId) ? InheritedZoneId : SelectedZoneId;
        Results.SelectedItem = results.FirstOrDefault(o => o.Id == current) ?? results.FirstOrDefault();
        NoResults.IsVisible = results.Count == 0;

        DropDownBorder.Width = SearchBox.Bounds.Width;
        DropDown.IsOpen = true;
        if (Results.SelectedItem != null) {
            Results.ScrollIntoView(Results.SelectedItem);
        }
    }

    private void SearchBox_GotFocus(object? sender, GotFocusEventArgs e) {
        if (DropDown.IsOpen) return;
        // Opening shows every zone, with the chosen one highlighted; typing narrows it down.
        OpenResults(null);
        SearchBox.SelectAll();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) {
        if (_showingSelection || !SearchBox.IsFocused) return;
        OpenResults(SearchBox.Text);
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e) {
        switch (e.Key) {
            case Key.Down or Key.Up: {
                if (!DropDown.IsOpen) {
                    OpenResults(null);
                } else if (Results.ItemCount > 0) {
                    var step = e.Key == Key.Down ? 1 : -1;
                    Results.SelectedIndex = System.Math.Clamp(Results.SelectedIndex + step, 0, Results.ItemCount - 1);
                    Results.ScrollIntoView(Results.SelectedIndex);
                }
                e.Handled = true;
                break;
            }
            case Key.Enter: {
                if (AllowNone && string.IsNullOrWhiteSpace(SearchBox.Text)) {
                    SetCurrentValue(SelectedZoneIdProperty, null);
                    Commit(null);
                } else if (DropDown.IsOpen) {
                    Commit(Results.SelectedItem as TimeZoneOption);
                }
                e.Handled = true;
                break;
            }
            case Key.Escape when DropDown.IsOpen:
                Commit(null);
                e.Handled = true;
                break;
        }
    }

    private void Results_Tapped(object? sender, TappedEventArgs e) {
        if (Results.SelectedItem is TimeZoneOption option) {
            Commit(option);
        }
    }

    /// <summary>
    ///     Runs after the focus move has settled. A click on an entry must not count as leaving the picker,
    ///     even if the popup takes focus while the mouse button is down.
    /// </summary>
    private void CloseWhenFocusLeft() {
        Dispatcher.UIThread.Post(() => {
            if (SearchBox.IsFocused || Results.IsKeyboardFocusWithin || DropDownBorder.IsPointerOver ||
                !DropDown.IsOpen) return;

            // An emptied box clears the zone (when allowed); anything else half-typed goes back to the chosen zone.
            if (AllowNone && string.IsNullOrWhiteSpace(SearchBox.Text)) {
                SetCurrentValue(SelectedZoneIdProperty, null);
            }
            Commit(null);
        }, DispatcherPriority.Input);
    }
}
