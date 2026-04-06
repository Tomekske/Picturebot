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
}
