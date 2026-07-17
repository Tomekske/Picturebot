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
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Domain.Enums;
using Domain.Interfaces;
using Graph.Domain.Interfaces;
using Domain.Messages;
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
    IRecipient<ProcessingCompletedMessage>,
    IRecipient<CurationCompletedMessage> {
    private readonly IAlbumService _albumService;
    private readonly IFolderService _folderService;
    private readonly ICurationQueue _curationQueue;
    private readonly ICopyService _copyService;
    private readonly IPictureGroupingService _groupingService;
    private readonly INavigationService _navigationService;
    private readonly INodeService _nodeService;
    private readonly IPathService _pathService;
    private readonly ISettingsService _settingsService;
    private readonly IXmpService _xmpService;
    private readonly HashSet<string> _pendingThumbnailRefreshes = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<PictureItemViewModel> _allPictures = new();
    private int _pendingAutoFlagBatchCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<CurationStatus> _filterStatuses = new();

    [ObservableProperty]
    private ObservableCollection<int> _filterRatings = new();

    [ObservableProperty]
    private ObservableCollection<ColorLabel> _filterColors = new();

    // Boolean properties for UI bindings
    public bool IsFlaggedFilterActive { get => FilterStatuses.Contains(CurationStatus.Flagged); set => ToggleStatusFilter(CurationStatus.Flagged, value); }
    public bool IsUnflaggedFilterActive { get => FilterStatuses.Contains(CurationStatus.Unflagged); set => ToggleStatusFilter(CurationStatus.Unflagged, value); }
    public bool IsRejectedFilterActive { get => FilterStatuses.Contains(CurationStatus.Rejected); set => ToggleStatusFilter(CurationStatus.Rejected, value); }

    public bool IsOneStarFilterActive { get => FilterRatings.Contains(1); set => ToggleRatingFilter(1, value); }
    public bool IsTwoStarFilterActive { get => FilterRatings.Contains(2); set => ToggleRatingFilter(2, value); }
    public bool IsThreeStarFilterActive { get => FilterRatings.Contains(3); set => ToggleRatingFilter(3, value); }
    public bool IsFourStarFilterActive { get => FilterRatings.Contains(4); set => ToggleRatingFilter(4, value); }
    public bool IsFiveStarFilterActive { get => FilterRatings.Contains(5); set => ToggleRatingFilter(5, value); }
    public bool IsZeroStarFilterActive { get => FilterRatings.Contains(0); set => ToggleRatingFilter(0, value); }

    public bool IsNoneColorFilterActive { get => FilterColors.Contains(ColorLabel.None); set => ToggleColorFilter(ColorLabel.None, value); }
    public bool IsRedColorFilterActive { get => FilterColors.Contains(ColorLabel.Red); set => ToggleColorFilter(ColorLabel.Red, value); }
    public bool IsOrangeColorFilterActive { get => FilterColors.Contains(ColorLabel.Orange); set => ToggleColorFilter(ColorLabel.Orange, value); }
    public bool IsYellowColorFilterActive { get => FilterColors.Contains(ColorLabel.Yellow); set => ToggleColorFilter(ColorLabel.Yellow, value); }
    public bool IsGreenColorFilterActive { get => FilterColors.Contains(ColorLabel.Green); set => ToggleColorFilter(ColorLabel.Green, value); }
    public bool IsBlueColorFilterActive { get => FilterColors.Contains(ColorLabel.Blue); set => ToggleColorFilter(ColorLabel.Blue, value); }
    public bool IsPinkColorFilterActive { get => FilterColors.Contains(ColorLabel.Pink); set => ToggleColorFilter(ColorLabel.Pink, value); }
    public bool IsPurpleColorFilterActive { get => FilterColors.Contains(ColorLabel.Purple); set => ToggleColorFilter(ColorLabel.Purple, value); }

    [ObservableProperty]
    private ObservableCollection<Node> _albumItems = new();

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCarouselCommand))]
    [NotifyCanExecuteChangedFor(nameof(GroupSimilarPicturesCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenInExplorerCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyToEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyToPrintCommand))]
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
        IAlbumService albumService, IFolderService folderService, ICopyService copyService,
        IXmpService xmpService) {
        _nodeService = nodeService;
        _pathService = pathService;
        _groupingService = groupingService;
        _navigationService = navigationService;
        _settingsService = settingsService;
        _curationQueue = curationQueue;
        _albumService = albumService;
        _folderService = folderService;
        _copyService = copyService;
        _xmpService = xmpService;

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
                picVm.Dispose();
                if (picVm.IsVisible) {
                    _ = picVm.LoadThumbnailAsync(320);
                }
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

    public async void Receive(NodeSelectedMessage message) {
        if (message.Value is Album album) {
            await LoadAlbumAsync(album);
        } else {
            UpdateGallery(message.Value);
        }
    }

    [RelayCommand]
    private void AutoFlagBestPictures() {
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

        _pendingAutoFlagBatchCount += bestPictures.Count;

        foreach (var picVm in bestPictures) {
            // Update to Flagged status
            picVm.CurationStatus = CurationStatus.Flagged;
            picVm.Picture.CurationStatus = CurationStatus.Flagged;

            // Enqueue for background persistence and Picked sync
            _curationQueue.Enqueue(picVm.Picture);
        }

        ApplyFilters();

        Log.Information("Auto-flagged {Count} best shots across burst groups. Waiting for background sync...", bestPictures.Count);

        MainWindow.ToastManager.CreateToast()
            .WithTitle("Syncing Curation")
            .WithContent($"Syncing {bestPictures.Count} best shots to database and Picked folders...")
            .Dismiss().After(TimeSpan.FromSeconds(2))
            .Queue();
    }

    public void Receive(CurationCompletedMessage message) {
        if (_pendingAutoFlagBatchCount <= 0) {
            return;
        }

        _pendingAutoFlagBatchCount -= message.Count;
        
        if (_pendingAutoFlagBatchCount > 0) {
            Log.Information("Curation sync progress: {Count} items remaining in current auto-flag batch.", _pendingAutoFlagBatchCount);
            return;
        }

        Log.Information("Curation sync batch complete.");
        
        Dispatcher.UIThread.Post(() => {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Sync Complete")
                .WithContent("Successfully synced best shots to database and Picked folders.")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        });

        _pendingAutoFlagBatchCount = 0;
    }

    [RelayCommand]
    private async Task ToggleGroupingMode() {
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

        var groupVms = new List<PictureGroupViewModel>();
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
            groupVms.Add(groupVm);
        }

        GroupedPictures = new ObservableCollection<PictureGroupViewModel>(groupVms);
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
        var groupVms = new List<PictureGroupViewModel>();

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
            groupVms.Add(new PictureGroupViewModel(header, header,
                new ObservableCollection<PictureItemViewModel>(group), true));
        }

        GroupedPictures = new ObservableCollection<PictureGroupViewModel>(groupVms);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteGroupSimilar))]
    private async Task GroupSimilarPictures() {
        IsBurstViewEnabled = true;
        await RefreshGalleryGrouping();
    }

    private bool CanExecuteGroupSimilar() => CanPlayCarousel && CanExecuteShortcuts();

    [RelayCommand(CanExecute = nameof(CanExecuteOpenInExplorer))]
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

    private bool CanExecuteOpenInExplorer() => CanPlayCarousel && CanExecuteShortcuts();

    [RelayCommand(CanExecute = nameof(CanExecuteSyncPicked))]
    private async Task SyncCurationStatusWithPickedFolderAsync() {
        if (_currentNode is not Album album) return;

        try {
            await _albumService.SyncPickedStatusAsync(album);

            // Re-fetch children to get the updated entities and refresh the view
            var children = await _nodeService.FindChildrenAsync(album.Id);
            var pics = children.OfType<Picture>().ToList();
            foreach (var pic in pics) {
                pic.Parent = album;
            }
            _pathService.PopulatePaths(pics);

            await Task.Run(async () => {
                foreach (var pic in pics) {
                    await _xmpService.LoadMetadataAsync(pic);
                }
            });

            UpdateGalleryItems(album, children);
            
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Sync Complete")
                .WithContent($"Successfully synchronized curation status with the Picked folder for '{album.Name}'.")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to sync curation status with Picked folder for album {AlbumId}", album.Id);
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Sync Error")
                .WithContent("Failed to synchronize with Picked folder.")
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    private bool CanExecuteSyncPicked() => CanPlayCarousel && CanExecuteShortcuts();

    [RelayCommand(CanExecute = nameof(CanExecutePlayCarousel))]
    private void PlayCarousel() {
        var window = new CarouselWindow();
        var carouselVm =
            new CarouselDialogViewModel(PicturesList, SelectedPicture, _nodeService, _curationQueue, _copyService, window.Close);
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

    [RelayCommand(CanExecute = nameof(CanExecutePlayCarousel))]
    private async Task CopyToEdit() {
        if (SelectedPicture == null) return;

        try {
            var result = await _copyService.CopyToEditAsync(SelectedPicture.Picture);
            if (!result) {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Copy skipped")
                    .WithContent("File already exists in the destination folder.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            } else {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent($"Copied {SelectedPicture.Name} RAW to edit folder.")
                    .Dismiss().After(TimeSpan.FromSeconds(2))
                    .Queue();
            }
        } catch (Exception ex) {
            Log.Error(ex, "Failed to copy to edit folder");
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent("Failed to copy file to edit folder.")
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecutePlayCarousel))]
    private async Task CopyToPrint() {
        if (SelectedPicture == null) return;

        try {
            var result = await _copyService.CopyToPrintAsync(SelectedPicture.Picture);
            if (!result) {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Copy skipped")
                    .WithContent("File already exists in the destination folder.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            } else {
                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent($"Copied {SelectedPicture.Name} JPG to print folder.")
                    .Dismiss().After(TimeSpan.FromSeconds(2))
                    .Queue();
            }
        } catch (Exception ex) {
            Log.Error(ex, "Failed to copy to print folder");
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent("Failed to copy file to print folder.")
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    private bool CanExecutePlayCarousel() => CanPlayCarousel && CanExecuteShortcuts();

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
            TearDownXmpWatcher();
            _ = LoadInitialItemsAsync();
            return;
        }

        if (node is not Album) {
            TearDownXmpWatcher();
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

        var oldPictures = _allPictures.ToList();
        _allPictures.Clear();
        PicturesList.Clear();
        GroupedPictures.Clear();

        _ = Task.Run(() => {
            foreach (var picVm in oldPictures) {
                picVm.PropertyChanged -= OnPictureItemPropertyChanged;
                picVm.Dispose();
            }
        });

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
                    picVm.ResolveThumbnailPath();
                    picVm.PropertyChanged += OnPictureItemPropertyChanged;
                    _allPictures.Add(picVm);
                }

                bool hasPicked = _allPictures.Any(p => p.CurationStatus == CurationStatus.Flagged);
                if (hasPicked) {
                    FilterStatuses.Clear();
                    FilterStatuses.Add(CurationStatus.Flagged);
                } else {
                    FilterStatuses.Clear();
                    FilterRatings.Clear();
                    FilterColors.Clear();
                }

                ApplyFilters();
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

    private void OnPictureItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(PictureItemViewModel.CurationStatus) ||
            e.PropertyName == nameof(PictureItemViewModel.Rating) ||
            e.PropertyName == nameof(PictureItemViewModel.ColorLabel)) {
            ApplyFilters();
        }
    }

    private void ToggleStatusFilter(CurationStatus status, bool isActive) {
        if (isActive && !FilterStatuses.Contains(status)) FilterStatuses.Add(status);
        else if (!isActive) FilterStatuses.Remove(status);
        ApplyFilters();
    }

    private void ToggleRatingFilter(int rating, bool isActive) {
        if (isActive && !FilterRatings.Contains(rating)) FilterRatings.Add(rating);
        else if (!isActive) FilterRatings.Remove(rating);
        ApplyFilters();
    }

    private void ToggleColorFilter(ColorLabel color, bool isActive) {
        if (isActive && !FilterColors.Contains(color)) FilterColors.Add(color);
        else if (!isActive) FilterColors.Remove(color);
        ApplyFilters();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void ShowPickedOnly() {
        FilterStatuses.Clear();
        FilterRatings.Clear();
        FilterColors.Clear();
        FilterStatuses.Add(CurationStatus.Flagged);
        ApplyFilters();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void ClearAllFilters() {
        FilterStatuses.Clear();
        FilterRatings.Clear();
        FilterColors.Clear();
        ApplyFilters();
    }

    private bool CanExecuteShortcuts() {
        var focusManager = MainWindow.Instance?.FocusManager;
        var focused = focusManager?.GetFocusedElement();
        return focused is not TextBox && focused is not NumericUpDown && focused is not ComboBox;
    }

    private void ApplyFilters() {
        var filtered = _allPictures.AsEnumerable();

        if (FilterStatuses.Any()) {
            filtered = filtered.Where(p => FilterStatuses.Contains(p.CurationStatus));
        }

        if (FilterRatings.Any()) {
            filtered = filtered.Where(p => FilterRatings.Contains(p.Rating));
        }

        if (FilterColors.Any()) {
            filtered = filtered.Where(p => FilterColors.Contains(p.ColorLabel));
        }

        var filteredList = filtered.ToList();

        PicturesList = new ObservableCollection<PictureItemViewModel>(filteredList);

        if (SelectedPicture != null && !PicturesList.Contains(SelectedPicture)) {
            SelectedPicture = PicturesList.FirstOrDefault();
        }

        // Notify UI about filter state changes
        OnPropertyChanged(nameof(IsFlaggedFilterActive));
        OnPropertyChanged(nameof(IsUnflaggedFilterActive));
        OnPropertyChanged(nameof(IsRejectedFilterActive));

        OnPropertyChanged(nameof(IsOneStarFilterActive));
        OnPropertyChanged(nameof(IsTwoStarFilterActive));
        OnPropertyChanged(nameof(IsThreeStarFilterActive));
        OnPropertyChanged(nameof(IsFourStarFilterActive));
        OnPropertyChanged(nameof(IsFiveStarFilterActive));
        OnPropertyChanged(nameof(IsZeroStarFilterActive));

        OnPropertyChanged(nameof(IsNoneColorFilterActive));
        OnPropertyChanged(nameof(IsRedColorFilterActive));
        OnPropertyChanged(nameof(IsOrangeColorFilterActive));
        OnPropertyChanged(nameof(IsYellowColorFilterActive));
        OnPropertyChanged(nameof(IsGreenColorFilterActive));
        OnPropertyChanged(nameof(IsBlueColorFilterActive));
        OnPropertyChanged(nameof(IsPinkColorFilterActive));
        OnPropertyChanged(nameof(IsPurpleColorFilterActive));

        _ = RefreshGalleryGrouping();
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

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void SetCurationStatus(CurationStatus status) {
        if (SelectedPicture == null) {
            return;
        }

        try {
            SelectedPicture.Picture.CurationStatus = status;
            SelectedPicture.CurationStatus = status;
            _curationQueue.Enqueue(SelectedPicture.Picture);
            ApplyFilters();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update curation status in gallery for {Name}", SelectedPicture.Name);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void SetColorLabel(ColorLabel label) {
        if (SelectedPicture == null) {
            return;
        }

        try {
            SelectedPicture.Picture.ColorLabel = label;
            SelectedPicture.ColorLabel = label;
            _curationQueue.Enqueue(SelectedPicture.Picture);
            ApplyFilters();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update color label in gallery for {Name}", SelectedPicture.Name);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void SetRating(string ratingStr) {
        if (SelectedPicture == null || !int.TryParse(ratingStr, out var rating)) {
            return;
        }

        try {
            SelectedPicture.Picture.Rating = rating;
            SelectedPicture.Rating = rating;
            _curationQueue.Enqueue(SelectedPicture.Picture);
            ApplyFilters();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update rating in gallery for {Name}", SelectedPicture.Name);
        }
    }

    public void Receive(ProcessingProgressMessage message) {
        if (_currentNode?.Id != message.Value.AlbumId) {
            return;
        }

        lock (_pendingThumbnailRefreshes) {
            _pendingThumbnailRefreshes.Add(message.Value.CurrentItemName);
            if (!_refreshTimer.IsEnabled) {
                Dispatcher.UIThread.Post(() => {
                    if (!_refreshTimer.IsEnabled) {
                        _refreshTimer.Start();
                    }
                });
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

    [RelayCommand(CanExecute = nameof(IsShowingAlbum))]
    private async Task CreateXmpFiles() {
        if (_currentNode is not Album album) {
            return;
        }

        try {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Generating XMP Files")
                .WithContent($"Generating XMP sidecar files for album '{album.Name}'...")
                .Dismiss().After(TimeSpan.FromSeconds(2))
                .Queue();

            await _xmpService.CreateXmpForAlbumAsync(album.Id);

            // Re-load the current items to reflect the newly created XMP files
            await LoadAlbumAsync(album);

            MainWindow.ToastManager.CreateToast()
                .WithTitle("Success")
                .WithContent($"Successfully created XMP sidecars using legacy data for album '{album.Name}'.")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to create XMP files for album {AlbumName}", album.Name);
            
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent($"Failed to generate XMP files: {ex.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(4))
                .Queue();
        }
    }

    private FileSystemWatcher? _xmpWatcher;

    private Task LoadAlbumAsync(Album album) {
        SetupXmpWatcher(album);

        _currentNode = album;
        IsBurstViewEnabled = false;
        IsLibraryRoot = false;

        // Clear UI collections immediately to indicate loading
        Items.Clear();
        FolderItems.Clear();
        AlbumItems.Clear();

        // 1. Offload disposal of old view models to a background thread to prevent blocking the UI
        var oldPictures = _allPictures.ToList();
        _allPictures.Clear();
        PicturesList.Clear();
        GroupedPictures.Clear();

        _ = Task.Run(() => {
            foreach (var picVm in oldPictures) {
                picVm.PropertyChanged -= OnPictureItemPropertyChanged;
                picVm.Dispose();
            }
        });

        IsShowingAlbum = true;
        CanPlayCarousel = false;
        IsLoading = true;

        UpdateBreadcrumbs(album);

        // 2. Offload the entire load process (database query, Stage 1 loading, Stage 2 loading) to background
        _ = Task.Run(async () => {
            var children = await _nodeService.FindChildrenAsync(album.Id);
            var pics = children.OfType<Picture>().ToList();

            if (pics.Count == 0) {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    if (_currentNode?.Id == album.Id) {
                        IsLoading = false;
                    }
                });
                return;
            }

            // --- STAGE 1: Load first batch in parallel background task ---
            var initialBatchSize = Math.Min(24, pics.Count);
            var firstBatchPics = pics.Take(initialBatchSize).ToList();

            // Populate paths
            foreach (var pic in firstBatchPics) {
                pic.Parent = album;
            }
            _pathService.PopulatePaths(firstBatchPics);

            // Load XMP metadata in parallel first
            await Task.WhenAll(firstBatchPics.Select(pic => _xmpService.LoadMetadataAsync(pic)));

            // Create ViewModels
            var initialPicsList = new List<PictureItemViewModel>();
            foreach (var pic in firstBatchPics) {
                var picVm = new PictureItemViewModel(pic);
                picVm.ResolveThumbnailPath();
                picVm.PropertyChanged += OnPictureItemPropertyChanged;
                initialPicsList.Add(picVm);
            }

            // Filter
            var hasPickedInitial = initialPicsList.Any(p => p.CurationStatus == CurationStatus.Flagged);
            var initialFilterStatuses = new List<CurationStatus>();
            if (hasPickedInitial) {
                initialFilterStatuses.Add(CurationStatus.Flagged);
            }

            var filteredInitial = initialPicsList.AsEnumerable();
            if (initialFilterStatuses.Any()) {
                filteredInitial = filteredInitial.Where(p => initialFilterStatuses.Contains(p.CurationStatus));
            }
            var filteredListInitial = filteredInitial.ToList();

            // Date Grouping
            var groupVmsInitial = new List<PictureGroupViewModel>();
            var groupsInitial = filteredListInitial.GroupBy(p => p.Picture.CapturedAt.Date).OrderBy(g => g.Key);
            foreach (var group in groupsInitial) {
                var dateStr = group.Key.ToString("yyyy-MM-dd");
                var header = $"{dateStr} ({group.Count()})";
                var sortedGroup = group.OrderBy(p => p.Picture.CapturedAt).ToList();
                
                foreach (var pic in sortedGroup) {
                    pic.GroupName = null;
                    pic.BurstIndex = 0;
                    pic.BurstPosition = 0;
                    pic.BurstTotal = 0;
                }

                var groupVm = new PictureGroupViewModel(dateStr, header,
                    new ObservableCollection<PictureItemViewModel>(sortedGroup));
                groupVmsInitial.Add(groupVm);
            }

            // Post initial batch to UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (_currentNode?.Id != album.Id) {
                    // Switch occurred, discard view models
                    foreach (var picVm in initialPicsList) {
                        picVm.PropertyChanged -= OnPictureItemPropertyChanged;
                        picVm.Dispose();
                    }
                    return;
                }

                _allPictures.AddRange(initialPicsList);
                PicturesList = new ObservableCollection<PictureItemViewModel>(filteredListInitial);
                GroupedPictures = new ObservableCollection<PictureGroupViewModel>(groupVmsInitial);

                FilterStatuses.Clear();
                if (hasPickedInitial) {
                    FilterStatuses.Add(CurationStatus.Flagged);
                } else {
                    FilterRatings.Clear();
                    FilterColors.Clear();
                }

                CanPlayCarousel = pics.Any();
                IsLoading = pics.Count > initialBatchSize;

                NotifyFilterStates();
            });

            if (pics.Count <= initialBatchSize) {
                return;
            }

            // Introduce breathing room delay so the user sees the first batch fade in smoothly
            await Task.Delay(250);

            // --- STAGE 2: Load the remaining images in the background in chunks ---
            var remainingPics = pics.Skip(initialBatchSize).ToList();
            int chunkSize = 32; // Process 32 pictures at a time with metadata and thumbnails pre-loaded

            for (int i = 0; i < remainingPics.Count; i += chunkSize) {
                // Check if current node changed during processing
                bool shouldCancel = false;
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                    if (_currentNode?.Id != album.Id) {
                        shouldCancel = true;
                    }
                });
                if (shouldCancel) {
                    break;
                }

                var chunk = remainingPics.Skip(i).Take(chunkSize).ToList();

                // Populate paths
                foreach (var pic in chunk) {
                    pic.Parent = album;
                }
                _pathService.PopulatePaths(chunk);

                // Load XMP metadata in parallel background threads with capped concurrency
                await Parallel.ForEachAsync(chunk, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (pic, token) => {
                    await _xmpService.LoadMetadataAsync(pic);
                });

                // Create ViewModels
                var chunkVms = new List<PictureItemViewModel>();
                foreach (var pic in chunk) {
                    var picVm = new PictureItemViewModel(pic);
                    picVm.ResolveThumbnailPath();
                    picVm.PropertyChanged += OnPictureItemPropertyChanged;
                    chunkVms.Add(picVm);
                }

                // Post chunk to UI thread and merge into existing collections
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                    if (_currentNode?.Id != album.Id) {
                        // Switch occurred, discard chunk ViewModels
                        foreach (var picVm in chunkVms) {
                            picVm.PropertyChanged -= OnPictureItemPropertyChanged;
                            picVm.Dispose();
                        }
                        return;
                    }

                    _allPictures.AddRange(chunkVms);

                    // Re-apply filters on chunk
                    var filteredChunk = chunkVms.AsEnumerable();
                    if (FilterStatuses.Any()) {
                        filteredChunk = filteredChunk.Where(p => FilterStatuses.Contains(p.CurationStatus));
                    }
                    if (FilterRatings.Any()) {
                        filteredChunk = filteredChunk.Where(p => FilterRatings.Contains(p.Rating));
                    }
                    if (FilterColors.Any()) {
                        filteredChunk = filteredChunk.Where(p => FilterColors.Contains(p.ColorLabel));
                    }
                    var filteredChunkList = filteredChunk.ToList();

                    // Add to flat PicturesList
                    foreach (var picVm in filteredChunkList) {
                        PicturesList.Add(picVm);
                    }

                    // Add to GroupedPictures
                    var groups = filteredChunkList.GroupBy(p => p.Picture.CapturedAt.Date);
                    foreach (var group in groups) {
                        var dateStr = group.Key.ToString("yyyy-MM-dd");
                        
                        // Find existing group
                        var existingGroup = GroupedPictures.FirstOrDefault(g => g.Date == dateStr);
                        if (existingGroup != null) {
                            // Merge into existing group by appending (since pics are already sorted chronologically)
                            foreach (var pic in group) {
                                pic.GroupName = null;
                                pic.BurstIndex = 0;
                                pic.BurstPosition = 0;
                                pic.BurstTotal = 0;
                                existingGroup.Pictures.Add(pic);
                            }
                            existingGroup.Header = $"{dateStr} ({existingGroup.Pictures.Count})";
                        } else {
                            // Create new group
                            var header = $"{dateStr} ({group.Count()})";
                            var sortedGroup = group.OrderBy(p => p.Picture.CapturedAt).ToList();
                            foreach (var pic in sortedGroup) {
                                pic.GroupName = null;
                                pic.BurstIndex = 0;
                                pic.BurstPosition = 0;
                                pic.BurstTotal = 0;
                            }
                            var groupVm = new PictureGroupViewModel(dateStr, header,
                                new ObservableCollection<PictureItemViewModel>(sortedGroup));
                            
                            // Insert group in sorted order
                            int insertIdx = 0;
                            while (insertIdx < GroupedPictures.Count && 
                                   string.Compare(GroupedPictures[insertIdx].Date, dateStr, StringComparison.Ordinal) < 0) {
                                insertIdx++;
                            }
                            GroupedPictures.Insert(insertIdx, groupVm);
                        }
                    }
                });

                // Yield control to UI thread to process renders/events
                await Task.Delay(35);
            }

            // Set IsLoading to false after all chunks are processed
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                if (_currentNode?.Id == album.Id) {
                    IsLoading = false;
                    NotifyFilterStates();
                }
            });
        });

        return Task.CompletedTask;
    }

    private void NotifyFilterStates() {
        OnPropertyChanged(nameof(IsFlaggedFilterActive));
        OnPropertyChanged(nameof(IsUnflaggedFilterActive));
        OnPropertyChanged(nameof(IsRejectedFilterActive));
        OnPropertyChanged(nameof(IsOneStarFilterActive));
        OnPropertyChanged(nameof(IsTwoStarFilterActive));
        OnPropertyChanged(nameof(IsThreeStarFilterActive));
        OnPropertyChanged(nameof(IsFourStarFilterActive));
        OnPropertyChanged(nameof(IsFiveStarFilterActive));
        OnPropertyChanged(nameof(IsZeroStarFilterActive));
        OnPropertyChanged(nameof(IsNoneColorFilterActive));
        OnPropertyChanged(nameof(IsRedColorFilterActive));
        OnPropertyChanged(nameof(IsOrangeColorFilterActive));
        OnPropertyChanged(nameof(IsYellowColorFilterActive));
        OnPropertyChanged(nameof(IsGreenColorFilterActive));
        OnPropertyChanged(nameof(IsBlueColorFilterActive));
        OnPropertyChanged(nameof(IsPinkColorFilterActive));
        OnPropertyChanged(nameof(IsPurpleColorFilterActive));
    }

    private void SetupXmpWatcher(Album album) {
        TearDownXmpWatcher();

        if (string.IsNullOrEmpty(_settingsService.Current.LibraryPath) || string.IsNullOrEmpty(album.Uuid)) {
            return;
        }

        var rawsPath = Path.Combine(_settingsService.Current.LibraryPath, album.Uuid, "RAWs");
        if (!Directory.Exists(rawsPath)) {
            return;
        }

        try {
            _xmpWatcher = new FileSystemWatcher(rawsPath, "*.xmp") {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _xmpWatcher.Changed += OnXmpFileChanged;
            _xmpWatcher.Created += OnXmpFileChanged;
            _xmpWatcher.Deleted += OnXmpFileChanged;
            _xmpWatcher.Renamed += OnXmpFileRenamed;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to start FileSystemWatcher for album {Uuid} at {Path}", album.Uuid, rawsPath);
        }
    }

    private void TearDownXmpWatcher() {
        if (_xmpWatcher != null) {
            _xmpWatcher.EnableRaisingEvents = false;
            _xmpWatcher.Changed -= OnXmpFileChanged;
            _xmpWatcher.Created -= OnXmpFileChanged;
            _xmpWatcher.Deleted -= OnXmpFileChanged;
            _xmpWatcher.Renamed -= OnXmpFileRenamed;
            _xmpWatcher.Dispose();
            _xmpWatcher = null;
        }
    }

    private void OnXmpFileChanged(object sender, FileSystemEventArgs e) {
        var fileName = Path.GetFileNameWithoutExtension(e.Name);
        if (string.IsNullOrEmpty(fileName)) return;

        var fileDir = Path.GetDirectoryName(e.FullPath);

        Dispatcher.UIThread.Post(async () => {
            if (_currentNode is not Album activeAlbum || string.IsNullOrEmpty(activeAlbum.Uuid)) {
                return;
            }

            if (string.IsNullOrEmpty(_settingsService.Current.LibraryPath)) {
                return;
            }

            var activeRawsPath = Path.Combine(_settingsService.Current.LibraryPath, activeAlbum.Uuid, "RAWs");
            if (!activeRawsPath.Equals(fileDir, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var picVm = _allPictures.FirstOrDefault(p => p.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (picVm == null) return;

            if (e.ChangeType == WatcherChangeTypes.Deleted) {
                picVm.Picture.CurationStatus = CurationStatus.Unflagged;
                picVm.Picture.ColorLabel = ColorLabel.None;
                picVm.Picture.Rating = 0;

                picVm.CurationStatus = CurationStatus.Unflagged;
                picVm.ColorLabel = ColorLabel.None;
                picVm.Rating = 0;
            } else {
                var originalCapturedAt = picVm.Picture.CapturedAt;

                await _xmpService.LoadMetadataAsync(picVm.Picture);

                picVm.CurationStatus = picVm.Picture.CurationStatus;
                picVm.ColorLabel = picVm.Picture.ColorLabel;
                picVm.Rating = picVm.Picture.Rating;

                if (picVm.Picture.CapturedAt != originalCapturedAt) {
                    await _nodeService.UpdateNodeAsync(picVm.Picture);
                    _ = RefreshGalleryGrouping();
                }
            }

            ApplyFilters();
        });
    }

    private void OnXmpFileRenamed(object sender, RenamedEventArgs e) {
        var oldName = Path.GetFileNameWithoutExtension(e.OldName);
        var newName = Path.GetFileNameWithoutExtension(e.Name);
        var fileDir = Path.GetDirectoryName(e.FullPath);

        Dispatcher.UIThread.Post(async () => {
            if (_currentNode is not Album activeAlbum || string.IsNullOrEmpty(activeAlbum.Uuid)) {
                return;
            }

            if (string.IsNullOrEmpty(_settingsService.Current.LibraryPath)) {
                return;
            }

            var activeRawsPath = Path.Combine(_settingsService.Current.LibraryPath, activeAlbum.Uuid, "RAWs");
            if (!activeRawsPath.Equals(fileDir, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            if (!string.IsNullOrEmpty(oldName)) {
                var oldPicVm = _allPictures.FirstOrDefault(p => p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
                if (oldPicVm != null) {
                    oldPicVm.Picture.CurationStatus = CurationStatus.Unflagged;
                    oldPicVm.Picture.ColorLabel = ColorLabel.None;
                    oldPicVm.Picture.Rating = 0;

                    oldPicVm.CurationStatus = CurationStatus.Unflagged;
                    oldPicVm.ColorLabel = ColorLabel.None;
                    oldPicVm.Rating = 0;
                }
            }

            if (!string.IsNullOrEmpty(newName)) {
                var newPicVm = _allPictures.FirstOrDefault(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                if (newPicVm != null) {
                    var originalCapturedAt = newPicVm.Picture.CapturedAt;

                    await _xmpService.LoadMetadataAsync(newPicVm.Picture);

                    newPicVm.CurationStatus = newPicVm.Picture.CurationStatus;
                    newPicVm.ColorLabel = newPicVm.Picture.ColorLabel;
                    newPicVm.Rating = newPicVm.Picture.Rating;

                    if (newPicVm.Picture.CapturedAt != originalCapturedAt) {
                        await _nodeService.UpdateNodeAsync(newPicVm.Picture);
                        _ = RefreshGalleryGrouping();
                    }
                }
            }

            ApplyFilters();
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
