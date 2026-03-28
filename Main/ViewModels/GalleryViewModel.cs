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
    
    [ObservableProperty]
    private ObservableCollection<Node> _items = new();

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    public GalleryViewModel(INodeService nodeService) {
        _nodeService = nodeService;
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
        Items.Clear();
        if (children != null) {
            foreach (var child in children.Where(n => n is Folder || n is Album)) {
                Items.Add(child);
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
