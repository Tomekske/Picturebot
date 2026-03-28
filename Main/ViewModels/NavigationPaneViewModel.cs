using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Main.Messages;

namespace Main.ViewModels;

public partial class NavigationPaneViewModel : ViewModelBase {
    private readonly INodeService _nodeService;

    [ObservableProperty]
    private ObservableCollection<NavigationNodeViewModel> _folders = new();

    public NavigationPaneViewModel(INodeService nodeService) {
        _nodeService = nodeService;
        _ = LoadFoldersAsync();

        WeakReferenceMessenger.Default.Register<NavigationPaneViewModel, FolderCreatedMessage>(this,
            (r, m) => _ = r.LoadFoldersAsync());
    }


    public void Receive(FolderCreatedMessage message) {
        // Refresh the navigation pane
        _ = LoadFoldersAsync();
    }

    public async Task LoadFoldersAsync() {
        var roots = await _nodeService.LoadHydratedTreeAsync();

        await Dispatcher.UIThread.InvokeAsync(() => {
            Folders.Clear();
            foreach (var root in roots) {
                if (root is Folder || root is Album) {
                    Folders.Add(new NavigationNodeViewModel(root));
                }
            }
        });
    }
}
