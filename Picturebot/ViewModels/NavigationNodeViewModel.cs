using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Picturebot.Messages;

namespace Picturebot.ViewModels;

public partial class NavigationNodeViewModel : ViewModelBase {
    private readonly Node _node;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public NavigationNodeViewModel(Node node) {
        _node = node;
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

    public int Id { get; }
    public string Name { get; }
    public bool IsFolder { get; }
    public bool IsAlbum { get; }

    public ObservableCollection<NavigationNodeViewModel> Children { get; } = new();

    partial void OnIsSelectedChanged(bool value) {
        if (value) {
            WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(_node));
        }
    }
}
