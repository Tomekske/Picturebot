using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;

namespace Picturebot.Controls;

/// <summary>
/// Shows one picture of <see cref="Items"/> at a time and slides between them.
/// Two image slots take turns (current and incoming), so every move costs the same,
/// including wrapping from the first picture to the last.
/// </summary>
public class GpuAcceleratedCarousel : TemplatedControl {
    public static readonly StyledProperty<IList?> ItemsProperty =
        AvaloniaProperty.Register<GpuAcceleratedCarousel, IList?>(nameof(Items));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<GpuAcceleratedCarousel, int>(nameof(SelectedIndex), 0);

    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(250);

    public IList? Items {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public int SelectedIndex {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private Image? _front; // shows SelectedIndex, resting at X = 0
    private Image? _back;  // parked off-screen, used for the next incoming picture
    private int _frontIndex = -1;
    private int _backIndex = -1;
    private CancellationTokenSource? _slideCts;

    static GpuAcceleratedCarousel() {
        SelectedIndexProperty.Changed.AddClassHandler<GpuAcceleratedCarousel>((x, e) => x.OnSelectedIndexChanged(e));
        ItemsProperty.Changed.AddClassHandler<GpuAcceleratedCarousel>((x, e) => x.OnItemsChanged(e));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        _front = e.NameScope.Find<Image>("PART_SlotA");
        _back = e.NameScope.Find<Image>("PART_SlotB");
        foreach (var slot in new[] { _front, _back }) {
            if (slot != null) slot.RenderTransform = new TranslateTransform();
        }
        ShowWithoutAnimation(SelectedIndex);
    }

    private void OnItemsChanged(AvaloniaPropertyChangedEventArgs e) {
        if (e.OldValue is INotifyCollectionChanged oldCollection) {
            oldCollection.CollectionChanged -= OnCollectionChanged;
        }
        if (e.NewValue is INotifyCollectionChanged newCollection) {
            newCollection.CollectionChanged += OnCollectionChanged;
        }
        ShowWithoutAnimation(SelectedIndex);
    }

    // Previews load lazily: when the picture a slot is showing arrives, swap it in.
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (e.Action != NotifyCollectionChangedAction.Replace || e.NewItems == null) {
            ShowWithoutAnimation(SelectedIndex);
            return;
        }

        for (var i = 0; i < e.NewItems.Count; i++) {
            var index = e.NewStartingIndex + i;
            var source = e.NewItems[i] as IImage;
            if (index == _frontIndex && _front != null && _front.Source != source) {
                _front.Source = source;
                if (source != null) FadeIn(_front);
            } else if (index == _backIndex && _back != null) {
                _back.Source = source;
            }
        }
    }

    private void OnSelectedIndexChanged(AvaloniaPropertyChangedEventArgs e) {
        var count = Items?.Count ?? 0;
        var to = (int)(e.NewValue ?? 0);
        if (count == 0 || _front == null || _back == null || _frontIndex < 0 || Bounds.Width <= 0) {
            ShowWithoutAnimation(to);
            return;
        }
        if (to == _frontIndex) return;

        // Direction on the circular list: wrapping counts as a single step
        var from = _frontIndex;
        int direction;
        if (to == (from + 1) % count) direction = 1;
        else if (to == (from - 1 + count) % count) direction = -1;
        else direction = to > from ? 1 : -1;

        SlideTo(to, direction);
    }

    private void SlideTo(int index, int direction) {
        if (_front == null || _back == null) return;

        _slideCts?.Cancel();
        _slideCts = new CancellationTokenSource();
        var token = _slideCts.Token;
        var width = Bounds.Width;

        // Incoming picture enters from the side we're moving towards; the current one leaves the other way
        var incoming = _back;
        var outgoing = _front;
        incoming.Source = ItemAt(index);
        incoming.Opacity = 1;

        _ = Slide(incoming, direction * width, 0, token);
        _ = Slide(outgoing, 0, -direction * width, token);

        _back = outgoing;
        _backIndex = _frontIndex;
        _front = incoming;
        _frontIndex = index;
    }

    // Local X is set to the end value first, so a cancelled or finished animation rests there.
    private static System.Threading.Tasks.Task Slide(Image slot, double fromX, double toX, CancellationToken token) {
        if (slot.RenderTransform is TranslateTransform t) t.X = toX;
        var animation = new Animation {
            Duration = SlideDuration,
            Easing = new CubicEaseOut(),
            Children = {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.XProperty, fromX) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.XProperty, toX) } }
            }
        };
        return animation.RunAsync(slot, token);
    }

    private static void FadeIn(Image slot) {
        slot.Opacity = 1;
        var animation = new Animation {
            Duration = FadeDuration,
            Children = {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 1d) } }
            }
        };
        _ = animation.RunAsync(slot);
    }

    private void ShowWithoutAnimation(int index) {
        if (_front == null || _back == null) return;

        _slideCts?.Cancel();
        var count = Items?.Count ?? 0;
        _frontIndex = count == 0 ? -1 : Math.Clamp(index, 0, count - 1);
        _backIndex = -1;

        _front.Source = _frontIndex >= 0 ? ItemAt(_frontIndex) : null;
        _back.Source = null;
        ParkSlots();
    }

    private void ParkSlots() {
        if (_front?.RenderTransform is TranslateTransform f) f.X = 0;
        if (_back?.RenderTransform is TranslateTransform b) b.X = Bounds.Width;
    }

    private IImage? ItemAt(int index) {
        var items = Items;
        return items != null && index >= 0 && index < items.Count ? items[index] as IImage : null;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        base.OnSizeChanged(e);
        _slideCts?.Cancel();
        ParkSlots();
    }
}
