using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Domain.Enums;
using Domain.Interfaces;
using Graph.Domain.Interfaces;
using Picturebot.Messages;
using Picturebot.Services;
using Picturebot.Views;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class GalleryViewModel : ViewModelBase, 
    IRecipient<NodeSelectedMessage>, 
    IRecipient<NodeCreatedMessage>,
    IRecipient<NodeDeletedMessage>,
    IRecipient<NodeUpdatedMessage>,
    IRecipient<ProcessingProgressMessage>,
    IRecipient<ProcessingCompletedMessage> {
    private readonly IAlbumService _albumService;
    private readonly IFolderService _folderService;
    private readonly ICurationQueue _curationQueue;
    private readonly IPictureGroupingService _groupingService;
    private readonly INavigationService _navigationService;
    private readonly INodeService _nodeService;
    private readonly IPathService _pathService;
    private readonly ISettingsService _settingsService;
    private readonly HashSet<string> _pendingThumbnailRefreshes = new();
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private ObservableCollection<Node> _albumItems = new();

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCarouselCommand))]
    [NotifyCanExecuteChangedFor(nameof(GroupSimilarPicturesCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenInExplorerCommand))]
    private bool _canPlayCarousel;

    private Node? _currentNode;

    [ObservableProperty]
    private ObservableCollection<Node> _folderItems = new();

    [ObservableProperty]
    private ObservableCollection<PictureGroupViewModel> _groupedPictures = new();

    [ObservableProperty]
    private bool _isBurstViewEnabled;

    [ObservableProperty]
    private bool _isShowingAlbum;

    [ObservableProperty]
    private bool _isLibraryRoot;

    [ObservableProperty]
    private ObservableCollection<Node> _items = new();

    [ObservableProperty]
    private ObservableCollection<PictureItemViewModel> _picturesList = new();

    [ObservableProperty]
    private PictureItemViewModel? _selectedPicture;

    public GalleryViewModel(INodeService nodeService, IPathService pathService,
        IPictureGroupingService groupingService, INavigationService navigationService,
        ISettingsService settingsService, ICurationQueue curationQueue,
        IAlbumService albumService, IFolderService folderService) {
        _nodeService = nodeService;
        _pathService = pathService;
        _groupingService = groupingService;
        _navigationService = navigationService;
        _settingsService = settingsService;
        _curationQueue = curationQueue;
        _albumService = albumService;
        _folderService = folderService;

        _refreshTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500),
            DispatcherPriority.ApplicationIdle,
            (s, e) => ProcessPendingRefreshes());

        WeakReferenceMessenger.Default.RegisterAll(this);
        _ = LoadInitialItemsAsync();
    }

    private void ProcessPendingRefreshes() {
        List<string> itemsToRefresh;
        lock (_pendingThumbnailRefreshes) {
            itemsToRefresh = _pendingThumbnailRefreshes.ToList();
            _pendingThumbnailRefreshes.Clear();
            _refreshTimer.Stop();
        }

        foreach (var name in itemsToRefresh) {
            var picVm = PicturesList.FirstOrDefault(p => p.Name == name);
            if (picVm != null) {
                picVm.ProcessingState = ProcessingState.Completed;
                _ = picVm.LoadThumbnailAsync(250);
            }
        }
    }

    public void Receive(NodeCreatedMessage message) {
        var newNode = message.Value;

        if (newNode is not (Folder or Album)) {
            return;
        }

        // If we are in the parent folder, add the new node to the items list
        if (_currentNode?.Id == newNode.ParentId || (_currentNode == null && newNode.ParentId == null)) {
            if (!Items.Any(i => i.Id == newNode.Id)) {
                Items.Add(newNode);
                if (newNode is Folder) {
                    FolderItems.Add(newNode);
                } else if (newNode is Album) {
                    AlbumItems.Add(newNode);
                }
            }
        }
    }

    public void Receive(NodeDeletedMessage message) {
        var deletedNode = message.Value;

        // If we are in the parent folder, remove the deleted node
        if (_currentNode?.Id == deletedNode.ParentId || (_currentNode == null && deletedNode.ParentId == null)) {
            var itemToRemove = Items.FirstOrDefault(i => i.Id == deletedNode.Id);
            if (itemToRemove != null) {
                Items.Remove(itemToRemove);
                if (deletedNode is Folder) {
                    FolderItems.Remove(itemToRemove);
                } else if (deletedNode is Album) {
                    AlbumItems.Remove(itemToRemove);
                }
            }
        }
    }

    public void Receive(NodeUpdatedMessage message) {
        var updatedNode = message.Value;

        // If the updated node is the current node, we might need to refresh breadcrumbs or title
        if (_currentNode?.Id == updatedNode.Id) {
            _currentNode.Name = updatedNode.Name;
            _currentNode.ParentId = updatedNode.ParentId;
            UpdateBreadcrumbs(_currentNode);
        }

        // If we are in the parent folder of the updated node, refresh its name in the items list
        if (_currentNode?.Id == updatedNode.ParentId || (_currentNode == null && updatedNode.ParentId == null)) {
            var itemToUpdate = Items.FirstOrDefault(i => i.Id == updatedNode.Id);
            if (itemToUpdate != null) {
                var index = Items.IndexOf(itemToUpdate);
                if (index != -1) {
                    Items[index] = updatedNode;
                }
                
                if (updatedNode is Folder) {
                    var fIndex = FolderItems.IndexOf(itemToUpdate);
                    if (fIndex != -1) FolderItems[fIndex] = updatedNode;
                } else if (updatedNode is Album) {
                    var aIndex = AlbumItems.IndexOf(itemToUpdate);
                    if (aIndex != -1) AlbumItems[aIndex] = updatedNode;
                }
            } else {
                // It might have been moved INTO this folder
                Items.Add(updatedNode);
                if (updatedNode is Folder) FolderItems.Add(updatedNode);
                else if (updatedNode is Album) AlbumItems.Add(updatedNode);
            }
        } else {
            // It might have been moved OUT of this folder
            var itemToRemove = Items.FirstOrDefault(i => i.Id == updatedNode.Id);
            if (itemToRemove != null) {
                Items.Remove(itemToRemove);
                if (updatedNode is Folder) FolderItems.Remove(itemToRemove);
                else if (updatedNode is Album) AlbumItems.Remove(itemToRemove);
            }
        }
    }

    public void Receive(NodeSelectedMessage message) {
        UpdateGallery(message.Value);
    }

    [RelayCommand]
    private async Task AutoFlagBestPictures() {
        if (!IsBurstViewEnabled) {
            return;
        }

        var burstGroups = GroupedPictures.Where(g => g.IsBurstGroup).ToList();
        var bestPictures = burstGroups
            .SelectMany(g => g.Pictures)
            .Where(p => p.IsBest)
            .ToList();

        if (!bestPictures.Any()) {
            return;
        }

        foreach (var picVm in bestPictures) {
            // Update to Flagged status
            picVm.CurationStatus = CurationStatus.Flagged;
            picVm.Picture.CurationStatus = CurationStatus.Flagged;

            // Enqueue for background persistence and Picked sync
            _curationQueue.Enqueue(picVm.Picture);
        }

        Log.Information("Auto-flagged {Count} best shots across burst groups", bestPictures.Count);
    }

    [RelayCommand]
    private async Task ToggleGroupingMode() {
        IsBurstViewEnabled = !IsBurstViewEnabled;
        await RefreshGalleryGrouping();
    }

    private async Task RefreshGalleryGrouping() {
        if (!IsShowingAlbum || _currentNode == null) {
            return;
        }

        GroupedPictures.Clear();
        foreach (var pic in PicturesList) {
            pic.IsBest = false;
        }

        if (IsBurstViewEnabled) {
            await ApplyBurstGrouping();
        } else {
            ApplyDateGrouping();
        }
    }

    private void ApplyDateGrouping() {
        var groups = PicturesList.GroupBy(p => p.Picture.CapturedAt.Date)
            .OrderBy(g => g.Key);

        foreach (var group in groups) {
            var dateStr = group.Key.ToString("yyyy-MM-dd");
            var count = group.Count();
            var header = $"{dateStr} ({count})";
            var sortedGroup = group.OrderBy(p => p.Picture.CapturedAt);
            
            foreach (var pic in sortedGroup) {
                pic.GroupName = null;
                pic.BurstIndex = 0;
                pic.BurstPosition = 0;
                pic.BurstTotal = 0;
            }

            var groupVm = new PictureGroupViewModel(dateStr, header,
                new ObservableCollection<PictureItemViewModel>(sortedGroup));
            GroupedPictures.Add(groupVm);
        }
    }

    private Orientation GetOrientation(Picture p) {
        if (p.Width > p.Height) {
            return Orientation.Landscape;
        }

        if (p.Height > p.Width) {
            return Orientation.Portrait;
        }

        return Orientation.Square;
    }

    private int CalculateHammingDistance(ulong h1, ulong h2) {
        return BitOperations.PopCount(h1 ^ h2);
    }

    private async Task ApplyBurstGrouping() {
        if (_currentNode == null || PicturesList.Count == 0) {
            return;
        }

        // 1. Fetch Config with safe fallbacks
        var settings = _settingsService.Current;
        var timeThreshold = settings.BurstTimeThresholdSeconds > 0 ? settings.BurstTimeThresholdSeconds : 3;
        var fallbackThreshold = settings.BurstFallbackTimeThresholdSeconds > 0
            ? settings.BurstFallbackTimeThresholdSeconds
            : 10;
        var hashThreshold = settings.GroupingThreshold > 0 ? settings.GroupingThreshold : 8;

        // 2. Sort everything chronologically
        var sortedPics = PicturesList.OrderBy(p => p.Picture.CapturedAt).ToList();

        var burstGroups = new List<List<PictureItemViewModel>>();
        if (sortedPics.Count > 0) {
            var currentGroup = new List<PictureItemViewModel> { sortedPics[0] };

            // 3. Sliding window to catch bursts
            for (var i = 1; i < sortedPics.Count; i++) {
                var currentPic = sortedPics[i];
                var prevPic = sortedPics[i - 1];

                var timeDiff = (currentPic.Picture.CapturedAt - prevPic.Picture.CapturedAt).TotalSeconds;

                // Condition A: Orientation must match exactly
                var orientationMatches = GetOrientation(prevPic.Picture) == GetOrientation(currentPic.Picture);

                // Condition B: Strict time OR (Fallback time AND Hash similarity)
                var isSimilar = timeDiff <= timeThreshold ||
                                (timeDiff <= fallbackThreshold &&
                                 CalculateHammingDistance(prevPic.Picture.Hash, currentPic.Picture.Hash) <=
                                 hashThreshold);

                if (orientationMatches && isSimilar) {
                    currentGroup.Add(currentPic);
                } else {
                    burstGroups.Add(currentGroup);
                    currentGroup = new List<PictureItemViewModel> { currentPic };
                }
            }

            burstGroups.Add(currentGroup);
        }

        var groupIndex = 1;

        // 4. Process ALL groups as burst groups (even singletons)
        foreach (var group in burstGroups) {
            // Mark the sharpest photo as the 'Best' (if it's a singleton, it automatically wins)
            var bestPic = group.OrderByDescending(p => p.Picture.Sharpness).FirstOrDefault();
            if (bestPic != null) {
                bestPic.IsBest = true;
            }

            // Format the header dynamically depending on if it's a sequence or a single shot
            var countSuffix = group.Count > 1 ? $"{group.Count} photos" : "Single";
            var header = $"Burst {groupIndex} ({countSuffix})";

            var position = 1;
            foreach (var pic in group) {
                pic.GroupName = header;
                pic.BurstIndex = groupIndex;
                pic.BurstPosition = position++;
                pic.BurstTotal = group.Count;
            }

            groupIndex++;

            // Pass 'true' to ensure the UI treats every single one as a burst group
            GroupedPictures.Add(new PictureGroupViewModel(header, header,
                new ObservableCollection<PictureItemViewModel>(group), true));
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlayCarousel))]
    private async Task GroupSimilarPictures() {
        IsBurstViewEnabled = true;
        await RefreshGalleryGrouping();
    }

    [RelayCommand(CanExecute = nameof(CanPlayCarousel))]
    private void OpenInExplorer() {
        if (_currentNode is not Album album || string.IsNullOrEmpty(album.Uuid)) {
            return;
        }

        var libraryPath = _settingsService.Current.LibraryPath;
        if (string.IsNullOrEmpty(libraryPath)) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent("Library path is not configured.")
                .Dismiss().ByClicking()
                .Queue();
            return;
        }

        var albumPath = Path.Combine(libraryPath, album.Uuid);

        if (!Directory.Exists(albumPath)) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent("Album directory does not exist or is inaccessible.")
                .Dismiss().ByClicking()
                .Queue();
            return;
        }

        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Process.Start("explorer.exe", albumPath);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Process.Start("open", albumPath);
            } else {
                Process.Start("xdg-open", albumPath);
            }
        } catch (Exception ex) {
            Log.Error(ex, "Failed to open directory in file explorer: {Path}", albumPath);
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent("Failed to open File Explorer.")
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlayCarousel))]
    private void PlayCarousel() {
        var window = new CarouselWindow();
        var carouselVm =
            new CarouselDialogViewModel(PicturesList, SelectedPicture, _nodeService, _curationQueue, window.Close);
        window.DataContext = carouselVm;

        window.Closed += (s, e) => {
            if (window.DataContext is CarouselDialogViewModel cvm) {
                SelectedPicture = cvm.CurrentPicture;
            }
        };

        if (MainWindow.Instance != null) {
            window.Show(MainWindow.Instance);
        } else {
            window.Show();
        }
    }

    partial void OnSelectedPictureChanged(PictureItemViewModel? value) {
        foreach (var pic in PicturesList) {
            pic.IsSelected = pic == value;
        }

        WeakReferenceMessenger.Default.Send(new PictureSelectedMessage(value));
    }

    [RelayCommand]
    private async Task EditCurrentNodeAsync() {
        if (_currentNode == null) return;

        var allNodes = await _nodeService.LoadHydratedTreeAsync();
        var vm = new EditNodeDialogViewModel(_nodeService, _folderService, _currentNode, allNodes, result => {
            if (result != null) {
                // Broadcast update to refresh the UI elsewhere
                WeakReferenceMessenger.Default.Send(new NodeUpdatedMessage(result));
            }
        });

        MainWindow.DialogManager.CreateDialog()
            .WithContent(new EditNodeDialog { DataContext = vm })
            .TryShow();
    }

    [RelayCommand]
    private async Task DeleteCurrentNodeAsync() {
        if (_currentNode == null) return;

        var title = $"Delete {_currentNode.Type}";
        var message = _currentNode.Type == NodeType.Folder
            ? $"Are you sure you want to delete the folder '{_currentNode.Name}'? Only empty folders can be deleted."
            : $"Are you sure you want to delete the album '{_currentNode.Name}'? This will move the physical directory to the 'deleted' folder and remove all picture records from the database.";

        var vm = new ConfirmDeleteDialogViewModel(title, message, async result => {
            if (result) {
                try {
                    var nodeToDelete = _currentNode;
                    if (nodeToDelete is Folder folder) {
                        await _folderService.DeleteAsync(folder);
                    } else if (nodeToDelete is Album album) {
                        await _albumService.DeleteAsync(album);
                    }

                    Log.Information("{Type} deleted: {Name}", nodeToDelete.Type, nodeToDelete.Name);

                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Success")
                        .WithContent($"{nodeToDelete.Type} '{nodeToDelete.Name}' has been deleted.")
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Queue();

                    // Navigate back to parent
                    var parent = nodeToDelete.Parent;
                    _navigationService.NavigateTo(parent);

                    // Broadcast deletion to refresh the tree
                    WeakReferenceMessenger.Default.Send(new NodeDeletedMessage(nodeToDelete));
                } catch (Exception ex) {
                    Log.Error(ex, "Failed to delete {Type}", _currentNode.Type);
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

    private async Task LoadInitialItemsAsync() {
        var roots = await _nodeService.LoadHydratedTreeAsync();
        UpdateGalleryItems(null, roots);
    }

    private void UpdateGallery(Node? node) {
        if (node == null) {
            _ = LoadInitialItemsAsync();
            return;
        }

        UpdateGalleryItems(node, node.Children?.ToList());
    }

    private void UpdateGalleryItems(Node? currentNode, List<Node>? children) {
        _currentNode = currentNode;
        IsBurstViewEnabled = false;
        IsLibraryRoot = currentNode == null;

        // Clear collections to prevent ghosting
        Items.Clear();
        FolderItems.Clear();
        AlbumItems.Clear();

        foreach (var picVm in PicturesList) {
            picVm.Dispose();
        }

        PicturesList.Clear();
        GroupedPictures.Clear();

        IsShowingAlbum = currentNode is Album;
        CanPlayCarousel = IsShowingAlbum && children?.OfType<Picture>().Any() == true;

        if (children != null) {
            if (IsShowingAlbum) {
                var pics = children.OfType<Picture>()
                    .OrderBy(p => p.CapturedAt)
                    .ToList();

                _pathService.PopulatePaths(pics);

                foreach (var pic in pics) {
                    var picVm = new PictureItemViewModel(pic);
                    PicturesList.Add(picVm);
                    _ = picVm.LoadThumbnailAsync(250);
                }

                _ = RefreshGalleryGrouping();
            } else {
                var list = children.Where(n => n is Folder || n is Album).ToList();
                foreach (var child in list) {
                    Items.Add(child);
                    if (child is Folder) {
                        FolderItems.Add(child);
                    } else if (child is Album) {
                        AlbumItems.Add(child);
                    }
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
        _navigationService.NavigateTo(breadcrumb.Node);
    }

    [RelayCommand]
    private void NavigateToChild(Node node) {
        _navigationService.NavigateTo(node);
    }

    [RelayCommand]
    private async Task SetCurationStatus(CurationStatus status) {
        if (SelectedPicture == null) {
            return;
        }

        try {
            SelectedPicture.Picture.CurationStatus = status;
            SelectedPicture.CurationStatus = status;
            _curationQueue.Enqueue(SelectedPicture.Picture);
            await Task.CompletedTask;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update curation status in gallery for {Name}", SelectedPicture.Name);
        }
    }

    public void Receive(ProcessingProgressMessage message) {
        if (_currentNode?.Id != message.Value.AlbumId) {
            return;
        }

        lock (_pendingThumbnailRefreshes) {
            _pendingThumbnailRefreshes.Add(message.Value.CurrentItemName);
            if (!_refreshTimer.IsEnabled) {
                _refreshTimer.Start();
            }
        }
    }

    public void Receive(ProcessingCompletedMessage message) {
        if (_currentNode?.Id != message.Value) {
            return;
        }

        Log.Information("Processing completed for current album {Id}, refreshing gallery.", message.Value);
        Dispatcher.UIThread.Post(() => {
            _ = RefreshGalleryGrouping();
        });
    }

    private enum Orientation {
        Landscape,
        Portrait,
        Square
    }
}

public class BreadcrumbItem(string name, Node? node) {
    public string Name { get; } = name;
    public Node? Node { get; } = node;
    public bool IsLast { get; set; }
}
