using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class GalleryView : UserControl {
    private readonly Queue<PictureItemViewModel> _loadedThumbnailsQueue = new();

    public GalleryView() {
        InitializeComponent();
        DataContextChanged += (s, e) => _loadedThumbnailsQueue.Clear();
    }

    public GalleryView(GalleryViewModel viewModel) {
        InitializeComponent();
        DataContext = viewModel;
        DataContextChanged += (s, e) => _loadedThumbnailsQueue.Clear();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        var focusedElement = focusManager?.GetFocusedElement();
        if (focusedElement is TextBox || focusedElement is NumericUpDown || focusedElement is ComboBox) {
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is PictureItemViewModel pic) {
            if (DataContext is GalleryViewModel vm) {
                vm.SelectedPicture = pic;
            }
        }
    }

    private void OnImageEffectiveViewportChanged(object? sender, Avalonia.Layout.EffectiveViewportChangedEventArgs e) {
        if (sender is Control control && control.DataContext is PictureItemViewModel vm) {
            var isVisible = e.EffectiveViewport.Width > 0 && e.EffectiveViewport.Height > 0;
            vm.IsVisible = isVisible;

            if (isVisible) {
                _ = vm.LoadThumbnailAsync(250);

                if (!_loadedThumbnailsQueue.Contains(vm)) {
                    _loadedThumbnailsQueue.Enqueue(vm);
                }

                // If cache exceeds limit, clean up off-screen loaded items to reclaim memory
                if (_loadedThumbnailsQueue.Count > 120) {
                    var tempQueue = new Queue<PictureItemViewModel>();
                    while (_loadedThumbnailsQueue.Count > 0) {
                        var item = _loadedThumbnailsQueue.Dequeue();
                        
                        // If item is scrolled out of view and we have at least 100 items cached, unload it
                        if (!item.IsVisible && (tempQueue.Count + _loadedThumbnailsQueue.Count >= 100)) {
                            item.Dispose();
                        } else {
                            tempQueue.Enqueue(item);
                        }
                    }

                    // Re-populate our queue
                    foreach (var item in tempQueue) {
                        _loadedThumbnailsQueue.Enqueue(item);
                    }
                }
            }
        }
    }
}
