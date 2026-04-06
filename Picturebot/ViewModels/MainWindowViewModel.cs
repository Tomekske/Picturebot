using System;
using Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Picturebot.ViewModels;

public class MainWindowViewModel : ViewModelBase {
    public MainWindowViewModel(
        GalleryViewModel galleryVM,
        NavigationPaneViewModel navigationPaneVM,
        DetailsInspectorViewModel detailsInspectorVM,
        StatusBarViewModel statusBarVM,
        IConfiguration configuration) {
        GalleryVM = galleryVM;
        NavigationPaneVM = navigationPaneVM;
        DetailsInspectorVM = detailsInspectorVM;
        StatusBarVM = statusBarVM;

        // Toggle the badge based on the loaded environment
        var env = configuration["Environment"] ??
                  Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? AppEnvironment.Production;
        ShowDevBadge = env != AppEnvironment.Production;
    }

    public GalleryViewModel GalleryVM { get; }
    public NavigationPaneViewModel NavigationPaneVM { get; }
    public DetailsInspectorViewModel DetailsInspectorVM { get; }
    public StatusBarViewModel StatusBarVM { get; }

    // Toggle the "DEV" badge visibility
    public bool ShowDevBadge { get; }
}
