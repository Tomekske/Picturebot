namespace Main.ViewModels;

public class MainWindowViewModel : ViewModelBase {
    public MainWindowViewModel(GalleryViewModel galleryVM, NavigationPaneViewModel navigationPaneVM) {
        GalleryVM = galleryVM;
        NavigationPaneVM = navigationPaneVM;
    }

    // Expose the Gallery ViewModel as a property
    public GalleryViewModel GalleryVM { get; }
    
    // Expose the NavigationPane ViewModel as a property
    public NavigationPaneViewModel NavigationPaneVM { get; }
}
