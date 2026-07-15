using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class GalleryView : UserControl {
    public GalleryView() {
        InitializeComponent();
        SetupScrollInterception();
    }

    public GalleryView(GalleryViewModel viewModel) {
        InitializeComponent();
        DataContext = viewModel;
        SetupScrollInterception();
    }

    private void SetupScrollInterception() {
        var groupedPicturesItemsControl = this.FindControl<ItemsControl>("GroupedPicturesItemsControl");
        groupedPicturesItemsControl?.AddHandler(InputElement.PointerWheelChangedEvent, (sender, e) => {
            e.Handled = false;
        }, RoutingStrategies.Bubble, true);
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
                _ = vm.LoadThumbnailAsync(320);
            } else {
                vm.CancelLoading();
            }
        }
    }

    private void OnImageDataContextChanged(object? sender, EventArgs e) {
        if (sender is Control control) {
            if (control.Tag is PictureItemViewModel oldVm) {
                oldVm.CancelLoading();
            }
            control.Tag = control.DataContext as PictureItemViewModel;
        }
    }
}
