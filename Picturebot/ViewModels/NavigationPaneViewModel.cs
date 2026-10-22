using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Picturebot.Messages;
using Picturebot.Services;
using Picturebot.Views;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class NavigationPaneViewModel : ViewModelBase, 
    IRecipient<NodeCreatedMessage>,
    IRecipient<NodeDeletedMessage>,
    IRecipient<NodeUpdatedMessage> {
    private readonly IAlbumService _albumService;
    private readonly IFolderService _folderService;
    private readonly IImportAlbumsService _importAlbumsService;
    private readonly INodeService _nodeService;
    private readonly ISettingsService _settingsService;
    private readonly INavigationService _navigationService;
    private readonly IRecentLocationsService _recentLocations;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<NavigationNodeViewModel> _folders = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearchActive;

    public ObservableCollection<string> KeywordSuggestions { get; } = new();

    private readonly DispatcherTimer _searchDebounceTimer;

    public NavigationPaneViewModel(
        INodeService nodeService,
        IFolderService folderService,
        IAlbumService albumService,
        IImportAlbumsService importAlbumsService,
        ISettingsService settingsService,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
        INavigationService navigationService,
        IRecentLocationsService recentLocations) {
        _nodeService = nodeService;
        _folderService = folderService;
        _albumService = albumService;
        _importAlbumsService = importAlbumsService;
        _settingsService = settingsService;
        _scopeFactory = scopeFactory;
        _navigationService = navigationService;
        _recentLocations = recentLocations;
        _ = LoadFoldersAsync();
        RefreshKeywordSuggestions();

        _searchDebounceTimer = new DispatcherTimer {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchDebounceTimer.Tick += (s, e) => {
            _searchDebounceTimer.Stop();
            if (!string.IsNullOrWhiteSpace(SearchQuery)) {
                ExecuteSearch(SearchQuery);
            }
        };

        _settingsService.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(ISettingsService.Current)) {
                RefreshKeywordSuggestions();
            }
        };

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(NodeCreatedMessage message) {
        // Refresh the navigation pane
        _ = LoadFoldersAsync();
    }

    public void Receive(NodeDeletedMessage message) {
        // Refresh the navigation pane
        _ = LoadFoldersAsync();
    }

    public void Receive(NodeUpdatedMessage message) {
        // Refresh the navigation pane
        _ = LoadFoldersAsync();
    }

    /// <summary>
    ///     New Album/Folder dialog with the location picker. Opens at the given node, or at what the
    ///     gallery shows; for an album that is its parent folder.
    /// </summary>
    [RelayCommand]
    public void OpenNewNodeDialog(NavigationNodeViewModel? at) {
        var anchorNode = at?.Node ?? _navigationService.CurrentNode;
        var startFolderId = anchorNode switch {
            Folder folder => folder.Id,
            Album album => album.ParentId,
            _ => null
        };

        var vm = new NewNodeDialogViewModel(
            _folderService,
            _settingsService,
            _scopeFactory,
            _recentLocations,
            Folders.Select(f => f.Node),
            startFolderId,
            startAsAlbum: true,
            result => {
                if (result.CreatedFolders.Count > 0 || result.Node != null) {
                    WeakReferenceMessenger.Default.Send(new NodeCreatedMessage(result.Node ?? result.CreatedFolders[^1]));
                }

                if (result.Error != null) {
                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Import failed")
                        .WithContent(result.Error)
                        .Dismiss().ByClicking()
                        .Queue();
                    return;
                }

                if (result.IsBatchImport) {
                    _ = LoadFoldersAsync();
                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Success")
                        .WithContent("Batch import of albums has completed.")
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Queue();
                } else if (result.Node is Album album) {
                    WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(album));
                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Success")
                        .WithContent($"Album '{album.Name}' import has completed.")
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Queue();
                } else if (result.Node is Folder folder) {
                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Success")
                        .WithContent($"Folder '{folder.Name}' has been created.")
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Queue();
                }
            });

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new NewNodeDialog { DataContext = vm })
            .TryShow();
    }

    [RelayCommand]
    public async Task OpenImportAlbumsDialogAsync() {
        var folders = await _folderService.FindAllAsync();

        var vm = new BatchImportAlbumsDialogViewModel(
            _importAlbumsService,
            _settingsService,
            folders,
            async () => {
                // Refresh the tree
                await LoadFoldersAsync();

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent("Batch import of albums has completed.")
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        );

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new BatchImportAlbumsDialog { DataContext = vm })
            .TryShow();
    }

    [RelayCommand]
    public async Task EditNodeAsync(NavigationNodeViewModel? nodeVm) {
        if (nodeVm == null) return;
        var nodeToEdit = nodeVm.Node;
        if (nodeToEdit == null) return;

        var allNodes = await _nodeService.LoadHydratedTreeAsync();
        var vm = new EditNodeDialogViewModel(_nodeService, _folderService, nodeToEdit, allNodes, result => {
            if (result != null) {
                // Broadcast update to refresh the tree and gallery
                WeakReferenceMessenger.Default.Send(new NodeUpdatedMessage(result));
            }
        }, _settingsService.Current.DefaultTimeZoneId, _recentLocations);

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new EditNodeDialog { DataContext = vm })
            .TryShow();
    }

    [RelayCommand]
    public void DeleteNode(NavigationNodeViewModel? nodeVm) {
        if (nodeVm == null) return;
        var nodeToDelete = nodeVm.Node;
        if (nodeToDelete == null) return;

        var title = $"Delete {nodeToDelete.Type}";
        var message = nodeToDelete.Type == NodeType.Folder
            ? $"Are you sure you want to delete the folder '{nodeToDelete.Name}'? Only empty folders can be deleted."
            : $"Are you sure you want to delete the album '{nodeToDelete.Name}'? This will move the physical directory to the 'deleted' folder and remove all picture records from the database.";

        var vm = new ConfirmDeleteDialogViewModel(title, message, async result => {
            if (result) {
                try {
                    var parent = nodeToDelete.Parent;

                    if (nodeToDelete is Folder folder) {
                        await _folderService.DeleteAsync(folder);
                    } else if (nodeToDelete is Album album) {
                        await _albumService.DeleteAsync(album);
                    }

                    if (parent?.Children != null) {
                        var inMemoryChild = parent.Children.FirstOrDefault(c => c.Id == nodeToDelete.Id);
                        if (inMemoryChild != null) {
                            parent.Children.Remove(inMemoryChild);
                        }
                    }

                    Log.Information("{Type} deleted: {Name}", nodeToDelete.Type, nodeToDelete.Name);

                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Success")
                        .WithContent($"{nodeToDelete.Type} '{nodeToDelete.Name}' has been deleted.")
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Queue();

                    // If the node being deleted is the currently active node, navigate away AFTER deletion
                    if (_navigationService.CurrentNode?.Id == nodeToDelete.Id) {
                        _navigationService.NavigateTo(parent);
                    }

                    // Broadcast deletion to refresh the tree and gallery
                    WeakReferenceMessenger.Default.Send(new NodeDeletedMessage(nodeToDelete));
                } catch (Exception ex) {
                    Log.Error(ex, "Failed to delete {Type}", nodeToDelete.Type);
                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Error")
                        .WithContent(ex.Message)
                        .Dismiss().ByClicking()
                        .Queue();
                }
            }
        });

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new ConfirmDeleteDialog { DataContext = vm })
            .TryShow();
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

    [RelayCommand]
    public void ExecuteSearch(string? query = null) {
        _searchDebounceTimer.Stop();
        var searchTerm = (query ?? SearchQuery)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchTerm)) {
            ClearSearch();
            return;
        }

        IsSearchActive = true;
        if (!string.Equals(SearchQuery, searchTerm, StringComparison.Ordinal)) {
            SearchQuery = searchTerm;
        }
        WeakReferenceMessenger.Default.Send(new GlobalSearchMessage(searchTerm));
    }

    [RelayCommand]
    public void ClearSearch() {
        _searchDebounceTimer.Stop();
        SearchQuery = string.Empty;
        IsSearchActive = false;
        WeakReferenceMessenger.Default.Send(new GlobalSearchMessage(string.Empty));
    }

    partial void OnSearchQueryChanged(string value) {
        _searchDebounceTimer.Stop();
        if (string.IsNullOrWhiteSpace(value)) {
            if (IsSearchActive) {
                ClearSearch();
            }
        } else {
            _searchDebounceTimer.Start();
        }
    }

    public void RefreshKeywordSuggestions() {
        KeywordSuggestions.Clear();
        var set = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var settings = _settingsService.Current;
        if (settings == null) return;

        // 1. Master Tags
        foreach (var t in settings.MasterTags) {
            if (!string.IsNullOrWhiteSpace(t.Name)) {
                set.Add(t.Name);
            }
        }

        // 2. Hierarchy node paths
        foreach (var node in settings.HierarchyNodes) {
            CollectHierarchySuggestions(node, "", set);
        }

        // 3. Tag groups
        foreach (var group in settings.TagGroups) {
            if (!string.IsNullOrWhiteSpace(group.GroupName)) {
                set.Add(group.GroupName);
            }
        }

        foreach (var item in set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)) {
            KeywordSuggestions.Add(item);
        }
    }

    private static void CollectHierarchySuggestions(HierarchyNode node, string parentPath, System.Collections.Generic.HashSet<string> set) {
        var currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath} › {node.Name}";
        if (!string.IsNullOrWhiteSpace(node.Name)) {
            set.Add(node.Name);
        }
        if (!string.IsNullOrEmpty(currentPath) && currentPath.Contains('›')) {
            set.Add(currentPath);
        }
        foreach (var child in node.Children) {
            CollectHierarchySuggestions(child, currentPath, set);
        }
    }
}
