namespace Main.ViewModels;

public class MainWindowViewModel : ViewModelBase {
    public MainWindowViewModel(GalleryViewModel galleryVM, NavigationPaneViewModel navigationPaneVM, DetailsInspectorViewModel detailsInspectorVM) {
        GalleryVM = galleryVM;
        NavigationPaneVM = navigationPaneVM;
        DetailsInspectorVM = detailsInspectorVM;
    }

    // Expose the Gallery ViewModel as a property
    public GalleryViewModel GalleryVM { get; }
    
    // Expose the NavigationPane ViewModel as a property
    public NavigationPaneViewModel NavigationPaneVM { get; }

    // Expose the DetailsInspector ViewModel as a property
    public DetailsInspectorViewModel DetailsInspectorVM { get; }
}
