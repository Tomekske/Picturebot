using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;

namespace Main.ViewModels;

public partial class NavigationPaneViewModel : ViewModelBase {
    private readonly INodeService _nodeService;

    [ObservableProperty]
    private ObservableCollection<NavigationNodeViewModel> _folders = new();

    public NavigationPaneViewModel(INodeService nodeService) {
        _nodeService = nodeService;
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
