using Avalonia.Controls;
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

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is PictureItemViewModel pic) {
            if (DataContext is GalleryViewModel vm) {
                vm.SelectedPicture = pic;
            }
        }
    }
}
