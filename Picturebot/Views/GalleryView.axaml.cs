using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Picturebot.Messages;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class GalleryView : UserControl {
    private PictureItemViewModel? _lastAnchorPic;

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
        if (groupedPicturesItemsControl != null) {
            groupedPicturesItemsControl.AddHandler(InputElement.PointerWheelChangedEvent, (sender, e) => {
                e.Handled = false;
            }, RoutingStrategies.Bubble, true);

            groupedPicturesItemsControl.AddHandler(InputElement.PointerPressedEvent, OnGlobalPointerPressed, RoutingStrategies.Tunnel);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        var focusedElement = focusManager?.GetFocusedElement();
        if (focusedElement is TextBox || focusedElement is NumericUpDown || focusedElement is ComboBox) {
            return;
        }

        if (DataContext is GalleryViewModel vm) {
            // Select All (Ctrl+A)
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A) {
                vm.SelectAllOrNoneCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Deselect All (Escape)
            if (e.Key == Key.Escape) {
                var allPics = GetAllPictures(vm);
                foreach (var pic in allPics) {
                    pic.IsSelected = false;
                }
                vm.SelectedPictures.Clear();
                vm.UpdateActiveMode();
                WeakReferenceMessenger.Default.Send(new PictureSelectionChangedMessage(new List<PictureItemViewModel>()));
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    private static List<PictureItemViewModel> GetAllPictures(GalleryViewModel vm) {
        var visible = vm.GroupedPictures.SelectMany(g => g.Pictures);
        return vm.AllPictures
            .Union(visible)
            .Union(vm.PicturesList)
            .Distinct()
            .ToList();
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is not GalleryViewModel vm) return;

        var pointerProps = e.GetCurrentPoint(this).Properties;

        // CRITICAL: Do NOT intercept mouse navigation buttons (Back/Forward) or right-clicks
        if (pointerProps.IsXButton1Pressed || pointerProps.IsXButton2Pressed || pointerProps.IsRightButtonPressed) {
            return;
        }

        Visual? current = e.Source as Visual;
        PictureItemViewModel? clickedPic = null;
        bool isCheckBoxClick = false;

        while (current != null) {
            if (current is CheckBox) {
                isCheckBoxClick = true;
            }
            if (clickedPic == null && current is Control control && control.DataContext is PictureItemViewModel pic) {
                clickedPic = pic;
            }
            current = current.GetVisualParent();
        }

        if (clickedPic == null) return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        var allPics = GetAllPictures(vm);
        var visiblePictures = vm.GroupedPictures
            .SelectMany(g => g.Pictures)
            .Distinct()
            .ToList();

        if (!visiblePictures.Any()) {
            visiblePictures = allPics;
        }

        if (isCheckBoxClick) {
            // Rule 2: OnClick(Checkbox)
            clickedPic.IsSelected = !clickedPic.IsSelected;
            _lastAnchorPic = clickedPic;
            e.Handled = true;
            SyncSelectionState(vm, clickedPic, updateFocus: false);
            return;
        }

        e.Handled = true;

        if (isShift) {
            // Rule 4: OnShiftClick(ImageCard)
            // If AnchorItem is null, set AnchorItem = FirstItemInGallery
            if (_lastAnchorPic == null || !visiblePictures.Contains(_lastAnchorPic)) {
                _lastAnchorPic = visiblePictures.FirstOrDefault() ?? clickedPic;
            }

            int startIndex = visiblePictures.IndexOf(_lastAnchorPic);
            int endIndex = visiblePictures.IndexOf(clickedPic);

            if (startIndex >= 0 && endIndex >= 0) {
                int min = Math.Min(startIndex, endIndex);
                int max = Math.Max(startIndex, endIndex);

                // Add all items in the resolved range to SelectedItems
                for (int i = min; i <= max; i++) {
                    visiblePictures[i].IsSelected = true;
                }
            }

            SyncSelectionState(vm, clickedPic, updateFocus: true);
        } else if (isCtrl) {
            // Rule 3: OnCtrlClick(ImageCard) / OnCmdClick(ImageCard)
            // Toggle TargetItem inside SelectedItems
            clickedPic.IsSelected = !clickedPic.IsSelected;
            _lastAnchorPic = clickedPic;
            SyncSelectionState(vm, clickedPic, updateFocus: true);
        } else {
            // Normal Left Click: Select ONLY clicked picture (displays checkbox and contours in Single Mode)
            foreach (var pic in allPics) {
                pic.IsSelected = (pic == clickedPic);
            }
            _lastAnchorPic = clickedPic;
            SyncSelectionState(vm, clickedPic, updateFocus: true);
        }
    }

    private void SyncSelectionState(GalleryViewModel vm, PictureItemViewModel activePic, bool updateFocus) {
        var allPics = GetAllPictures(vm);
        var selectedList = allPics.Where(p => p.IsSelected).ToList();

        vm.SelectedPictures.Clear();
        foreach (var pic in selectedList) {
            vm.SelectedPictures.Add(pic);
        }

        if (updateFocus) {
            vm.SelectedPicture = activePic;
            WeakReferenceMessenger.Default.Send(new PictureSelectedMessage(activePic));
        } else if (vm.SelectedPicture == null || !allPics.Contains(vm.SelectedPicture)) {
            vm.SelectedPicture = activePic;
            WeakReferenceMessenger.Default.Send(new PictureSelectedMessage(activePic));
        }

        vm.UpdateActiveMode();
        WeakReferenceMessenger.Default.Send(new PictureSelectionChangedMessage(selectedList));
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        // Selection is handled via OnGlobalPointerPressed and TwoWay IsSelected bindings
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
