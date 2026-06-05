using Avalonia.Controls;
using Avalonia.Input;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class GalleryView : UserControl {
    public GalleryView() {
        InitializeComponent();
    }

    public GalleryView(GalleryViewModel viewModel) {
        InitializeComponent();
        DataContext = viewModel;
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
}
