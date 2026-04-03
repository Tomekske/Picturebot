using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Main.Messages;

namespace Main.ViewModels;

public partial class GalleryViewModel : ViewModelBase, IRecipient<NodeSelectedMessage> {
    private readonly INodeService _nodeService;
    private readonly IPathService _pathService;

    [ObservableProperty]
    private ObservableCollection<Node> _items = new();

    [ObservableProperty]
    private ObservableCollection<Picture> _picturesList = new();

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    [ObservableProperty]
    private bool _isShowingAlbum;

    public GalleryViewModel(INodeService nodeService, IPathService pathService) {
        _nodeService = nodeService;
        _pathService = pathService;
        WeakReferenceMessenger.Default.RegisterAll(this);
        _ = LoadInitialItemsAsync();
    }

    private async Task LoadInitialItemsAsync() {
        var roots = await _nodeService.LoadHydratedTreeAsync();
        UpdateGalleryItems(null, roots);
    }

    public void Receive(NodeSelectedMessage message) {
        UpdateGallery(message.Value);
    }

    private void UpdateGallery(Node node) {
        UpdateGalleryItems(node, node.Children?.ToList());
    }

    private void UpdateGalleryItems(Node? currentNode, List<Node>? children) {
        // Clear both collections to prevent ghosting
        Items.Clear();
        PicturesList.Clear();
        
        IsShowingAlbum = currentNode is Album;

        if (children != null) {
            if (IsShowingAlbum) {
                var pics = children.OfType<Picture>().ToList();
                _pathService.PopulatePaths(pics);
                foreach (var pic in pics) {
                    PicturesList.Add(pic);
                }
            } else {
                foreach (var child in children.Where(n => n is Folder || n is Album)) {
                    Items.Add(child);
                }
            }
        }

        UpdateBreadcrumbs(currentNode);
    }

    private void UpdateBreadcrumbs(Node? node) {
        var path = new List<BreadcrumbItem>();
        var current = node;

        while (current != null) {
            path.Insert(0, new BreadcrumbItem(current.Name, current));
            current = current.Parent;
        }

        // Always add root "Library"
        path.Insert(0, new BreadcrumbItem("Library", null));

        // Mark the last one
        if (path.Count > 0) {
            path.Last().IsLast = true;
        }

        Breadcrumbs.Clear();
        foreach (var item in path) {
            Breadcrumbs.Add(item);
        }
    }

    [RelayCommand]
    private async Task NavigateToBreadcrumb(BreadcrumbItem breadcrumb) {
        if (breadcrumb.Node == null) {
            await LoadInitialItemsAsync();
        } else {
            UpdateGallery(breadcrumb.Node);
            WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(breadcrumb.Node));
        }
    }

    [RelayCommand]
    private void NavigateToChild(Node node) {
        UpdateGallery(node);
        WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(node));
    }
}

public class BreadcrumbItem(string name, Node? node) {
    public string Name { get; } = name;
    public Node? Node { get; } = node;
    public bool IsLast { get; set; }
}
