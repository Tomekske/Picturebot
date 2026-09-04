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
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.DTOs;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Picturebot.Messages;
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
    private readonly IImportPicturesCommand _importCommand;
    private readonly INodeService _nodeService;
    private readonly ISettingsService _settingsService;
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
        IImportPicturesCommand importCommand,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory) {
        _nodeService = nodeService;
        _folderService = folderService;
        _albumService = albumService;
        _importAlbumsService = importAlbumsService;
        _settingsService = settingsService;
        _importCommand = importCommand;
        _scopeFactory = scopeFactory;
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

    [RelayCommand]
    public async Task OpenCreateFolderDialogAsync() {
        var folders = await _folderService.FindAllAsync();

        var vm = new CreateFolderDialogViewModel(_folderService, result => {
            if (result != null) {
                Log.Information("Folder created: {result}", result.Name);

                // Broadcast creation to refresh the tree
                WeakReferenceMessenger.Default.Send(new NodeCreatedMessage(result));

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent($"Folder '{result.Name}' has been created.")
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }, folders);

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new CreateFolderDialog { DataContext = vm })
            .TryShow();
    }

    [RelayCommand]
    public async Task OpenAddAlbumDialogAsync() {
        var folders = await _folderService.FindAllAsync();

        var vm = new AddAlbumDialogViewModel(_albumService, _importCommand, _settingsService, _scopeFactory, folders, result => {
            if (result != null) {
                Log.Information("Album creation process started/finished for: {result}", result.Name);

                // Broadcast creation to refresh the tree
                WeakReferenceMessenger.Default.Send(new NodeCreatedMessage(result));

                // Automatically navigate to the new album
                WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(result));

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent($"Album '{result.Name}' import has completed.")
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        });

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new AddAlbumDialog { DataContext = vm })
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
