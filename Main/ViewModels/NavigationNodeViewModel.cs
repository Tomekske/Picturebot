using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Database.Domain.Entities;

namespace Main.ViewModels;

public partial class NavigationNodeViewModel : ViewModelBase {
    public int Id { get; }
    public string Name { get; }
    public bool IsFolder { get; }
    public bool IsAlbum { get; }
    
    [ObservableProperty]
    private bool _isExpanded;
    
    [ObservableProperty]
    private bool _isSelected;
    
    public ObservableCollection<NavigationNodeViewModel> Children { get; } = new();

    public NavigationNodeViewModel(Node node) {
        Id = node.Id;
        Name = node.Name;
        IsFolder = node is Folder;
        IsAlbum = node is Album;
        
        if (node.Children != null) {
            foreach (var child in node.Children) {
                if (child is Folder || child is Album) {
                    Children.Add(new NavigationNodeViewModel(child));
                }
            }
        }
    }
}
