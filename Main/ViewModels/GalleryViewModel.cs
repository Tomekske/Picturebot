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
    private string _currentFolderName = "Library";

    public GalleryViewModel(INodeService nodeService) {
        _nodeService = nodeService;
        WeakReferenceMessenger.Default.RegisterAll(this);
        _ = LoadInitialItemsAsync();
    }

    private async Task LoadInitialItemsAsync() {
        var roots = await _nodeService.LoadHydratedTreeAsync();
        UpdateGalleryItems("Library", roots);
    }

    public void Receive(NodeSelectedMessage message) {
        UpdateGallery(message.Value);
    }

    private void UpdateGallery(Node node) {
        UpdateGalleryItems(node.Name, node.Children?.ToList());
    }

    private void UpdateGalleryItems(string folderName, System.Collections.Generic.List<Node>? children) {
        CurrentFolderName = folderName;
        Items.Clear();
        if (children != null) {
            foreach (var child in children.Where(n => n is Folder || n is Album)) {
                Items.Add(child);
            }
        }
    }

    [RelayCommand]
    private void NavigateToChild(Node node) {
        UpdateGallery(node);
        
        // Notify other components that this node is now the focus
        WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(node));
    }
}
