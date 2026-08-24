using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using Picturebot.Messages;
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

    private bool _isUpdatingSelection;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (_isUpdatingSelection) return;
        if (sender is not ListBox listBox) return;
        if (DataContext is not GalleryViewModel vm) return;

        try {
            _isUpdatingSelection = true;

            if (e.AddedItems.Count > 0) {
                // Clear selection in other listboxes so only one group has active selection
                var container = this.FindControl<ItemsControl>("GroupedPicturesItemsControl");
                if (container != null) {
                    foreach (var child in container.GetRealizedContainers()) {
                        var otherListBox = child.FindControl<ListBox>("GroupListBox");
                        if (otherListBox != null && otherListBox != listBox && otherListBox.SelectedItems.Count > 0) {
                            otherListBox.SelectedItems.Clear();
                        }
                    }
                }

                vm.SelectedPictures.Clear();
                foreach (var item in listBox.SelectedItems) {
                    if (item is PictureItemViewModel pic) {
                        vm.SelectedPictures.Add(pic);
                    }
                }
                vm.SelectedPicture = listBox.SelectedItem as PictureItemViewModel;
                WeakReferenceMessenger.Default.Send(new PictureSelectionChangedMessage(vm.SelectedPictures.ToList()));
            } else if (e.RemovedItems.Count > 0 && listBox.SelectedItems.Count == 0) {
                var activePic = vm.SelectedPicture;
                if (activePic != null && listBox.Items.Cast<object>().Contains(activePic)) {
                    vm.SelectedPictures.Clear();
                    vm.SelectedPicture = null;
                    WeakReferenceMessenger.Default.Send(new PictureSelectionChangedMessage(new List<PictureItemViewModel>()));
                }
            }
        } finally {
            _isUpdatingSelection = false;
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
                vm.Thumbnail = null;
            }
        }
    }

    private void OnImageDataContextChanged(object? sender, EventArgs e) {
        if (sender is Control control) {
            if (control.Tag is PictureItemViewModel oldVm) {
                oldVm.CancelLoading();
                oldVm.Thumbnail = null;
            }
            control.Tag = control.DataContext as PictureItemViewModel;
        }
    }

    private void BulkDeleteFlyout_Opened(object? sender, EventArgs e) {
        if (DataContext is GalleryViewModel vm) {
            vm.PopulateAlbumTagsForBulkDeleteCommand.Execute(null);
        }
    }
}
