using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;

namespace Picturebot.Controls;

public class GpuAcceleratedCarousel : TemplatedControl {
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<GpuAcceleratedCarousel, IEnumerable?>(nameof(Items));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<GpuAcceleratedCarousel, int>(nameof(SelectedIndex), 0);

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<GpuAcceleratedCarousel, double>(nameof(Zoom), 1.0);

    public IEnumerable? Items {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public int SelectedIndex {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public double Zoom {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    private Panel? _itemsContainer;
    private Compositor? _compositor;
    private readonly List<Control> _activeItems = new();

    static GpuAcceleratedCarousel() {
        SelectedIndexProperty.Changed.AddClassHandler<GpuAcceleratedCarousel>((x, e) => x.OnSelectedIndexChanged(e));
        ZoomProperty.Changed.AddClassHandler<GpuAcceleratedCarousel>((x, e) => x.OnZoomChanged(e));
        ItemsProperty.Changed.AddClassHandler<GpuAcceleratedCarousel>((x, e) => x.OnItemsChanged(e));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        _itemsContainer = e.NameScope.Find<Panel>("PART_ItemsContainer");
        
        if (_itemsContainer != null) {
            _itemsContainer.Opacity = 0; // Hide until first layout/offset is set
        }

        var selfVisual = ElementComposition.GetElementVisual(this);
        if (selfVisual != null) {
            _compositor = selfVisual.Compositor;
        }

        InvalidateItems();
    }

    private void OnItemsChanged(AvaloniaPropertyChangedEventArgs e) {
        if (e.OldValue is INotifyCollectionChanged oldCollection) {
            oldCollection.CollectionChanged -= OnCollectionChanged;
        }
        if (e.NewValue is INotifyCollectionChanged newCollection) {
            newCollection.CollectionChanged += OnCollectionChanged;
        }
        InvalidateItems();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (e.Action == NotifyCollectionChangedAction.Replace && e.NewItems != null && _itemsContainer != null) {
            for (int i = 0; i < e.NewItems.Count; i++) {
                int index = e.NewStartingIndex + i;
                if (index < _itemsContainer.Children.Count && _itemsContainer.Children[index] is Image img) {
                    var newSource = e.NewItems[i] as IImage;
                    if (img.Source != newSource) {
                        img.Source = newSource;
                        if (newSource != null) {
                            AnimateImageIn(img);
                        }
                    }
                }
            }
        } else {
            InvalidateItems();
        }
    }

    private void AnimateImageIn(Control target) {
        var visual = ElementComposition.GetElementVisual(target);
        if (visual == null || _compositor == null) return;

        visual.Opacity = 0.0f;
        var animation = _compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1.0f, 1.0f);
        animation.Duration = TimeSpan.FromMilliseconds(400);
        visual.StartAnimation("Opacity", animation);
    }

    private void InvalidateItems() {
        if (_itemsContainer == null) return;

        _itemsContainer.Children.Clear();
        _activeItems.Clear();

        if (Items == null) return;

        foreach (var item in Items) {
            var image = new Image {
                Source = item as IImage,
                Stretch = Stretch.Uniform,
                Opacity = item == null ? 0 : 1 // Hide until loaded
            };
            
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            
            _itemsContainer.Children.Add(image);
            _activeItems.Add(image);
        }

        Dispatcher.UIThread.Post(() => TransitionTo(SelectedIndex, false), DispatcherPriority.Loaded);
    }

    private void OnSelectedIndexChanged(AvaloniaPropertyChangedEventArgs e) {
        TransitionTo((int)(e.NewValue ?? 0));
    }

    private void OnZoomChanged(AvaloniaPropertyChangedEventArgs e) {
        ApplyZoom((double)(e.NewValue ?? 1.0));
    }

    public void TransitionTo(int index, bool animate = true) {
        if (_compositor == null || _itemsContainer == null || _activeItems.Count == 0) return;

        index = Math.Clamp(index, 0, _activeItems.Count - 1);
        float itemWidth = (float)Bounds.Width;
        
        if (itemWidth <= 0) return; // Wait for valid bounds

        float targetOffset = -index * itemWidth;

        var visual = ElementComposition.GetElementVisual(_itemsContainer);
        if (visual == null) return;

        if (animate) {
            var animation = _compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(1.0f, new Vector3(targetOffset, 0, 0));
            animation.Duration = TimeSpan.FromMilliseconds(500);
            animation.IterationCount = 1;
            
            visual.StartAnimation("Offset", animation);
        } else {
            visual.Offset = new Vector3(targetOffset, 0, 0);
            
            // First time stabilization: Fade in the whole container
            if (_itemsContainer.Opacity == 0) {
                var fadeIn = _compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(1.0f, 1.0f);
                fadeIn.Duration = TimeSpan.FromMilliseconds(250);
                visual.StartAnimation("Opacity", fadeIn);
                _itemsContainer.Opacity = 1; 
            }
        }
    }

    private void ApplyZoom(double zoom) {
        if (_itemsContainer == null) return;
        var visual = ElementComposition.GetElementVisual(_itemsContainer);
        if (visual == null) return;

        // Ensure we scale from the center of the current item
        visual.CenterPoint = new Vector3((float)(Bounds.Width / 2 + SelectedIndex * Bounds.Width), (float)(Bounds.Height / 2), 0);

        var animation = _compositor!.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1.0f, new Vector3((float)zoom, (float)zoom, 1.0f));
        animation.Duration = TimeSpan.FromMilliseconds(300);
        visual.StartAnimation("Scale", animation);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        base.OnSizeChanged(e);
        TransitionTo(SelectedIndex, false);
    }
}
