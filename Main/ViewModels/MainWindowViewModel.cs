namespace Main.ViewModels;

public class MainWindowViewModel : ViewModelBase {
    public MainWindowViewModel(GalleryViewModel galleryViewModel, GalleryViewModel galleryVM) {
        GalleryViewModel = galleryViewModel;
        GalleryVM = galleryVM;
    }

    public GalleryViewModel GalleryViewModel { get; }

    // Expose the Gallery ViewModel as a property
    public GalleryViewModel GalleryVM { get; }
}
