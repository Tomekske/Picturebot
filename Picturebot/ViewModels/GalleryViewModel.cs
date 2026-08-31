using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
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
using Graph.Infrastructure.Services;
using Domain.Messages;
using Microsoft.Extensions.DependencyInjection;
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
    IRecipient<CurationCompletedMessage>,
    IRecipient<PictureKeywordsChangedMessage>,
    IRecipient<GlobalSearchMessage> {
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
    private readonly IPickedService _pickedService;
    private readonly IFewShotTagDiscoveryService? _tagDiscoveryService;
    private readonly IGlobalExemplarCentroidService? _centroidService;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory? _scopeFactory;
    private CancellationTokenSource? _albumLoadCts;
    private readonly HashSet<string> _pendingThumbnailRefreshes = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<PictureItemViewModel> _allPictures = new();
    public IReadOnlyList<PictureItemViewModel> AllPictures => _allPictures;
    private int _pendingAutoFlagBatchCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isGlobalSearchActive;

    [ObservableProperty]
    private string _activeSearchQuery = string.Empty;

    [ObservableProperty]
    private FilterToolbarViewModel _filterToolbar;

    public ObservableCollection<CurationStatus> FilterStatuses => FilterToolbar.FilterStatuses;
    public ObservableCollection<int> FilterRatings => FilterToolbar.FilterRatings;
    public ObservableCollection<ColorLabel> FilterColors => FilterToolbar.FilterColors;

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

    public bool CanEditOrDeleteCurrentNode => !IsLibraryRoot && !IsGlobalSearchActive && _currentNode != null;

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

    [ObservableProperty]
    private ActiveMode _activeMode = ActiveMode.SingleMode;

    public bool IsMultiMode => ActiveMode == ActiveMode.MultiMode;
    public bool IsSingleMode => ActiveMode == ActiveMode.SingleMode;

    public ObservableCollection<PictureItemViewModel> SelectedPictures { get; } = new();

    public void UpdateActiveMode() {
        var newMode = SelectedPictures.Count >= 2 ? ActiveMode.MultiMode : ActiveMode.SingleMode;
        if (ActiveMode != newMode) {
            ActiveMode = newMode;
        }
        OnPropertyChanged(nameof(IsMultiMode));
        OnPropertyChanged(nameof(IsSingleMode));
    }

    public GalleryViewModel(INodeService nodeService, IPathService pathService,
        IPictureGroupingService groupingService, INavigationService navigationService,
        ISettingsService settingsService, ICurationQueue curationQueue,
        IAlbumService albumService, IFolderService folderService, ICopyService copyService,
        IXmpService xmpService, IPickedService pickedService,
        IFewShotTagDiscoveryService? tagDiscoveryService = null,
        IGlobalExemplarCentroidService? centroidService = null,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory? scopeFactory = null) {
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
        _pickedService = pickedService;
        _tagDiscoveryService = tagDiscoveryService;
        _centroidService = centroidService;
        _scopeFactory = scopeFactory;

        _filterToolbar = new FilterToolbarViewModel(ApplyFilters);

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

        // If we are in the parent folder or album, remove the deleted node
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

            var picToRemove = _allPictures.FirstOrDefault(p => p.Picture.Id == deletedNode.Id);
            if (picToRemove != null) {
                picToRemove.PropertyChanged -= OnPictureItemPropertyChanged;
                _allPictures.Remove(picToRemove);
                SelectedPictures.Remove(picToRemove);
                if (SelectedPicture?.Picture.Id == deletedNode.Id) {
                    SelectedPicture = SelectedPictures.FirstOrDefault();
                }
                ApplyFilters();
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
        if (IsGlobalSearchActive) {
            IsGlobalSearchActive = false;
            ActiveSearchQuery = string.Empty;
        }

        if (message.Value is Album album) {
            await LoadAlbumAsync(album);
        } else {
            UpdateGallery(message.Value);
        }
    }

    public async void Receive(GlobalSearchMessage message) {
        var query = message.Value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query)) {
            if (IsGlobalSearchActive) {
                IsGlobalSearchActive = false;
                ActiveSearchQuery = string.Empty;
                if (_currentNode is Album album) {
                    await LoadAlbumAsync(album);
                } else if (_currentNode != null) {
                    UpdateGallery(_currentNode);
                } else {
                    await LoadInitialItemsAsync();
                }
            }
            return;
        }

        await ExecuteGlobalSearchAsync(query);
    }

    public async Task ExecuteGlobalSearchAsync(string query) {
        _albumLoadCts?.Cancel();
        _albumLoadCts = new CancellationTokenSource();
        var cancellationToken = _albumLoadCts.Token;

        IsGlobalSearchActive = true;
        ActiveSearchQuery = query;
        IsShowingAlbum = true;
        IsBurstViewEnabled = false;
        IsLibraryRoot = false;

        // Clear UI collections
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

        IsLoading = true;
        CanPlayCarousel = false;

        // Breadcrumb: Library > Search: "{query}"
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbItem("Library", null));
        var searchBreadcrumb = new BreadcrumbItem($"Search: \"{query}\"", null) { IsLast = true };
        Breadcrumbs.Add(searchBreadcrumb);

        _ = Task.Run(async () => {
            try {
                cancellationToken.ThrowIfCancellationRequested();
                using var scope = _scopeFactory?.CreateScope();
                var nodeService = scope?.ServiceProvider.GetService<INodeService>() ?? _nodeService;
                var pathService = scope?.ServiceProvider.GetService<IPathService>() ?? _pathService;
                var xmpService = scope?.ServiceProvider.GetService<IXmpService>() ?? _xmpService;

                var normalizedQuery = KeywordChipViewModel.NormalizePath(query);
                var rawQuery = query.Trim();
                Log.Information("Global search started for '{Query}' (normalized: '{Normalized}')...", rawQuery, normalizedQuery);

                // 1. Fetch all nodes and pictures to ensure full library coverage
                var allNodes = await nodeService.GetAllNodesAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var albumMap = allNodes.OfType<Album>().ToDictionary(a => a.Id);
                var allPics = allNodes.OfType<Picture>().ToList();

                foreach (var pic in allPics) {
                    if (pic.Parent == null && pic.ParentId.HasValue && albumMap.TryGetValue(pic.ParentId.Value, out var parentAlbum)) {
                        pic.Parent = parentAlbum;
                    }
                }
                pathService.PopulatePaths(allPics);

                // 2. Fetch SQLite DB matches
                var dbPics = await nodeService.SearchPicturesGlobalAsync(normalizedQuery, cancellationToken);
                var matchedPicsDict = new Dictionary<int, Picture>();
                foreach (var p in dbPics) {
                    matchedPicsDict[p.Id] = p;
                }

                // 3. For any pictures not matched via SQLite (e.g. unindexed KeywordsJson), check XMP files
                var uncheckedPics = allPics.Where(p => !matchedPicsDict.ContainsKey(p.Id)).ToList();
                await Parallel.ForEachAsync(uncheckedPics, new ParallelOptions {
                    MaxDegreeOfParallelism = Math.Max(2, Math.Min(8, Environment.ProcessorCount)),
                    CancellationToken = cancellationToken
                }, async (pic, ct) => {
                    await xmpService.LoadMetadataAsync(pic);
                    bool isMatch = false;

                    if (pic.Keywords != null && pic.Keywords.Any(k =>
                        k.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ||
                        k.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        KeywordChipViewModel.NormalizePath(k).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(k.Trim(), rawQuery, StringComparison.OrdinalIgnoreCase))) {
                        isMatch = true;
                    } else if (!string.IsNullOrEmpty(pic.Name) && pic.Name.Contains(rawQuery, StringComparison.OrdinalIgnoreCase)) {
                        isMatch = true;
                    } else if (pic.Parent != null && !string.IsNullOrEmpty(pic.Parent.Name) && pic.Parent.Name.Contains(rawQuery, StringComparison.OrdinalIgnoreCase)) {
                        isMatch = true;
                    }

                    if (isMatch) {
                        lock (matchedPicsDict) {
                            matchedPicsDict[pic.Id] = pic;
                        }
                    }
                });

                var finalPics = matchedPicsDict.Values.ToList();
                Log.Information("Global search for '{Query}' found {Count} pictures across all albums.", rawQuery, finalPics.Count);

                cancellationToken.ThrowIfCancellationRequested();

                if (finalPics.Count == 0) {
                    Dispatcher.UIThread.Post(() => {
                        if (IsGlobalSearchActive && ActiveSearchQuery == query) {
                            _allPictures.Clear();
                            PicturesList.Clear();
                            GroupedPictures.Clear();
                            IsLoading = false;
                        }
                    });
                    return;
                }

                // Ensure XMP metadata is loaded for all final pictures
                await Task.WhenAll(finalPics.Select(pic => xmpService.LoadMetadataAsync(pic)));

                var picVms = finalPics.Select(p => new PictureItemViewModel(p)).ToList();
                foreach (var picVm in picVms) {
                    picVm.PropertyChanged += OnPictureItemPropertyChanged;
                }

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (!IsGlobalSearchActive || ActiveSearchQuery != query) {
                        return;
                    }

                    _allPictures.Clear();
                    foreach (var vm in picVms) {
                        _allPictures.Add(vm);
                    }

                    if (FilterToolbar != null) {
                        FilterToolbar.ClearAll();
                        FilterToolbar.UpdateAvailableTags(_allPictures);
                    }

                    ApplyFilters();
                    IsLoading = false;
                    CanPlayCarousel = PicturesList.Count > 0;
                });

                // Background thumbnail loading
                foreach (var picVm in picVms) {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = picVm.LoadThumbnailAsync(320);
                }
            } catch (OperationCanceledException) {
                // Cancelled
            } catch (Exception ex) {
                Log.Error(ex, "Error executing global search for '{Query}'", query);
                Dispatcher.UIThread.Post(() => IsLoading = false);
            }
        }, cancellationToken);
    }

    public void Receive(PictureKeywordsChangedMessage message) {
        if (FilterToolbar != null) {
            FilterToolbar.UpdateAvailableTags(_allPictures);
        }
        ApplyFilters();
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
        if (!IsShowingAlbum || (_currentNode == null && !IsGlobalSearchActive)) {
            return;
        }

        foreach (var pic in PicturesList) {
            pic.IsBest = false;
        }

        if (IsBurstViewEnabled && !IsGlobalSearchActive) {
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
                if (pic.Parent == null) {
                    pic.Parent = album;
                }
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

    [RelayCommand(CanExecute = nameof(CanExecuteSyncPicked))]
    private async Task SynchronizeHighlightsAsync() {
        if (_currentNode is not Album album) return;

        try {
            await _albumService.SyncHighlightsAsync(album);

            MainWindow.ToastManager.CreateToast()
                .WithTitle("Sync Complete")
                .WithContent($"Successfully synchronized highlights for '{album.Name}'.")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to sync highlights for album {AlbumId}", album.Id);
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Sync Error")
                .WithContent("Failed to synchronize highlights.")
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
        var targetVms = GetSelectedPicturesOrActive();
        if (!targetVms.Any()) return;

        int copiedCount = 0;
        int skippedCount = 0;

        foreach (var picVm in targetVms) {
            try {
                var result = await _copyService.CopyToEditAsync(picVm.Picture);
                if (result) copiedCount++;
                else skippedCount++;
            } catch (Exception ex) {
                Log.Error(ex, "Failed to copy {Name} to edit folder", picVm.Name);
            }
        }

        if (copiedCount > 0) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Success")
                .WithContent($"Copied {copiedCount} picture(s) RAW to edit folder.")
                .Dismiss().After(TimeSpan.FromSeconds(2))
                .Queue();
        } else if (skippedCount > 0) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Copy skipped")
                .WithContent("Selected file(s) already exist in the destination folder.")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecutePlayCarousel))]
    private async Task CopyToPrint() {
        var targetVms = GetSelectedPicturesOrActive();
        if (!targetVms.Any()) return;

        int copiedCount = 0;
        int skippedCount = 0;

        foreach (var picVm in targetVms) {
            try {
                var result = await _copyService.CopyToPrintAsync(picVm.Picture);
                if (result) copiedCount++;
                else skippedCount++;
            } catch (Exception ex) {
                Log.Error(ex, "Failed to copy {Name} to print folder", picVm.Name);
            }
        }

        if (copiedCount > 0) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Success")
                .WithContent($"Copied {copiedCount} picture(s) JPG to print folder.")
                .Dismiss().After(TimeSpan.FromSeconds(2))
                .Queue();
        } else if (skippedCount > 0) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Copy skipped")
                .WithContent("Selected file(s) already exist in the destination folder.")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        }
    }

    private bool CanExecutePlayCarousel() => CanPlayCarousel && CanExecuteShortcuts();

    partial void OnSelectedPictureChanged(PictureItemViewModel? value) {
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
                        if (_currentNode?.Id == album.Id) {
                            TearDownXmpWatcher();
                        }
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
        using var scope = _scopeFactory?.CreateScope();
        var nodeService = scope?.ServiceProvider.GetService<INodeService>() ?? _nodeService;
        var roots = await nodeService.LoadHydratedTreeAsync();
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
                    FilterToolbar.SetFlaggedOnly();
                } else {
                    FilterToolbar.ClearAll();
                }
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
        if (FilterToolbar != null) {
            FilterToolbar.UpdateAvailableTags(_allPictures);
        }
    }

    private void OnPictureItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(PictureItemViewModel.CurationStatus) ||
            e.PropertyName == nameof(PictureItemViewModel.Rating) ||
            e.PropertyName == nameof(PictureItemViewModel.ColorLabel)) {
            ApplyFilters();
        } else if (e.PropertyName == nameof(PictureItemViewModel.Keywords)) {
            if (FilterToolbar != null) {
                FilterToolbar.UpdateAvailableTags(_allPictures);
            }
            ApplyFilters();
        }
    }

    private void ToggleStatusFilter(CurationStatus status, bool isActive) {
        if (FilterToolbar == null) return;
        switch (status) {
            case CurationStatus.Flagged: FilterToolbar.IsFlaggedActive = isActive; break;
            case CurationStatus.Unflagged: FilterToolbar.IsNeutralActive = isActive; break;
            case CurationStatus.Rejected: FilterToolbar.IsRejectedActive = isActive; break;
        }
    }

    private void ToggleRatingFilter(int rating, bool isActive) {
        if (FilterToolbar == null) return;
        switch (rating) {
            case 0: FilterToolbar.IsStar0Active = isActive; break;
            case 1: FilterToolbar.IsStar1Active = isActive; break;
            case 2: FilterToolbar.IsStar2Active = isActive; break;
            case 3: FilterToolbar.IsStar3Active = isActive; break;
            case 4: FilterToolbar.IsStar4Active = isActive; break;
            case 5: FilterToolbar.IsStar5Active = isActive; break;
        }
    }

    private void ToggleColorFilter(ColorLabel color, bool isActive) {
        if (FilterToolbar == null) return;
        switch (color) {
            case ColorLabel.Green: FilterToolbar.IsGreenActive = isActive; break;
            case ColorLabel.Blue: FilterToolbar.IsBlueActive = isActive; break;
            case ColorLabel.Yellow: FilterToolbar.IsYellowOrangeActive = isActive; break;
            case ColorLabel.Orange: FilterToolbar.IsYellowOrangeActive = isActive; break;
            case ColorLabel.Red: FilterToolbar.IsRedActive = isActive; break;
            case ColorLabel.Purple: FilterToolbar.IsPurpleActive = isActive; break;
            case ColorLabel.None: FilterToolbar.IsNoneActive = isActive; break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void ShowPickedOnly() {
        FilterToolbar.SetFlaggedOnly();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void ClearAllFilters() {
        FilterToolbar.ClearAll();
    }

    private bool CanExecuteShortcuts() {
        var focusManager = MainWindow.Instance?.FocusManager;
        var focused = focusManager?.GetFocusedElement();
        return focused is not TextBox && focused is not NumericUpDown && focused is not ComboBox;
    }

    private IEnumerable<PictureItemViewModel> ApplyFilterPredicate(IEnumerable<PictureItemViewModel> source) {
        if (FilterToolbar == null) return source;
        var filtered = source;

        if (FilterStatuses.Any()) {
            filtered = filtered.Where(p => FilterStatuses.Contains(p.CurationStatus));
        }

        if (FilterRatings.Any()) {
            filtered = filtered.Where(p => p.Rating >= FilterRatings.Min());
        }

        if (FilterColors.Any()) {
            filtered = filtered.Where(p => FilterColors.Contains(p.ColorLabel));
        }

        if (FilterToolbar.IsTagFilterActive) {
            var selectedTagNames = FilterToolbar.AllTags.Where(t => t.IsSelected).Select(t => t.Name).ToList();
            if (selectedTagNames.Any()) {
                bool MatchesTag(PictureItemViewModel pic, string tag) {
                    if (pic.Keywords == null || pic.Keywords.Count == 0) return false;
                    var normalizedTag = KeywordChipViewModel.NormalizePath(tag);
                    return pic.Keywords.Any(k => {
                        if (string.Equals(k, tag, StringComparison.OrdinalIgnoreCase)) return true;
                        var normK = KeywordChipViewModel.NormalizePath(k);
                        if (string.Equals(normK, normalizedTag, StringComparison.OrdinalIgnoreCase)) return true;
                        var segs = normK.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        return segs.Any(s => string.Equals(s, tag.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(s, normalizedTag, StringComparison.OrdinalIgnoreCase));
                    });
                }

                if (FilterToolbar.IsMatchAll) {
                    filtered = filtered.Where(p => selectedTagNames.All(tag => MatchesTag(p, tag)));
                } else if (FilterToolbar.IsMatchNot) {
                    filtered = filtered.Where(p => !selectedTagNames.Any(tag => MatchesTag(p, tag)));
                } else {
                    filtered = filtered.Where(p => selectedTagNames.Any(tag => MatchesTag(p, tag)));
                }
            }
        }

        return filtered;
    }

    private void ApplyFilters() {
        if (FilterToolbar == null) return;
        var filtered = ApplyFilterPredicate(_allPictures);
        var filteredList = filtered.ToList();

        // Optimize: Only refresh list and grouping if the filtered content actually changed.
        // Changing properties (like rating) of pictures that are still within the filter should not trigger a full UI layout teardown/refresh.
        bool listChanged = PicturesList.Count != filteredList.Count;
        if (!listChanged) {
            for (int i = 0; i < filteredList.Count; i++) {
                if (PicturesList[i] != filteredList[i]) {
                    listChanged = true;
                    break;
                }
            }
        }

        if (listChanged || (GroupedPictures.Count == 0 && filteredList.Count > 0)) {
            PicturesList = new ObservableCollection<PictureItemViewModel>(filteredList);

            if (SelectedPicture != null && !PicturesList.Contains(SelectedPicture)) {
                SelectedPicture = PicturesList.FirstOrDefault();
            }

            _ = RefreshGalleryGrouping();
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

    public List<PictureItemViewModel> GetSelectedPicturesOrActive() {
        var targetVms = _allPictures.Where(p => p.IsSelected).ToList();
        if (!targetVms.Any()) {
            targetVms = SelectedPictures.Where(p => p != null).ToList();
        }
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }
        return targetVms;
    }

    [RelayCommand]
    public void SelectAllOrNone() {
        bool allSelected = _allPictures.Count > 0 && _allPictures.All(p => p.IsSelected);
        bool targetState = !allSelected;

        foreach (var pic in _allPictures) {
            pic.IsSelected = targetState;
        }

        SelectedPictures.Clear();
        if (targetState) {
            foreach (var pic in _allPictures) {
                SelectedPictures.Add(pic);
            }
            if (SelectedPicture == null || !SelectedPictures.Contains(SelectedPicture)) {
                SelectedPicture = SelectedPictures.FirstOrDefault();
            }
        }

        UpdateActiveMode();

        if (SelectedPicture != null) {
            WeakReferenceMessenger.Default.Send(new PictureSelectedMessage(SelectedPicture));
        }
        WeakReferenceMessenger.Default.Send(new PictureSelectionChangedMessage(SelectedPictures.ToList()));
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void SetCurationStatus(CurationStatus status) {
        var targetVms = GetSelectedPicturesOrActive();
        if (!targetVms.Any()) return;

        foreach (var picVm in targetVms) {
            try {
                picVm.CurationStatus = status;
                _curationQueue.Enqueue(picVm.Picture);
            } catch (Exception ex) {
                Log.Error(ex, "Failed to update curation status in gallery for {Name}", picVm.Name);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void SetColorLabel(ColorLabel label) {
        var targetVms = GetSelectedPicturesOrActive();
        if (!targetVms.Any()) return;

        foreach (var picVm in targetVms) {
            try {
                picVm.ColorLabel = label;
                _curationQueue.Enqueue(picVm.Picture);
            } catch (Exception ex) {
                Log.Error(ex, "Failed to update color label in gallery for {Name}", picVm.Name);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void SetRating(string ratingStr) {
        if (!int.TryParse(ratingStr, out var rating)) {
            return;
        }

        var targetVms = GetSelectedPicturesOrActive();
        if (!targetVms.Any()) return;

        foreach (var picVm in targetVms) {
            try {
                picVm.Rating = rating;
                _curationQueue.Enqueue(picVm.Picture);
            } catch (Exception ex) {
                Log.Error(ex, "Failed to update rating in gallery for {Name}", picVm.Name);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteShortcuts))]
    private void ToggleKeyword(string keyword) {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        var trimmed = keyword.Trim();

        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        foreach (var picVm in targetVms) {
            if (picVm.Keywords.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) {
                picVm.RemoveKeyword(trimmed);
            } else {
                picVm.AddKeyword(trimmed);
            }
            _curationQueue.Enqueue(picVm.Picture);
        }

        WeakReferenceMessenger.Default.Send(new PictureSelectionChangedMessage(targetVms));
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
        _albumLoadCts?.Cancel();
        _albumLoadCts = new CancellationTokenSource();
        var cancellationToken = _albumLoadCts.Token;

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
            using var scope = _scopeFactory?.CreateScope();
            var nodeService = scope?.ServiceProvider.GetService<INodeService>() ?? _nodeService;
            var pathService = scope?.ServiceProvider.GetService<IPathService>() ?? _pathService;
            var xmpService = scope?.ServiceProvider.GetService<IXmpService>() ?? _xmpService;
            var pickedService = scope?.ServiceProvider.GetService<IPickedService>() ?? _pickedService;

            var children = await nodeService.FindChildrenAsync(album.Id);
            var pics = children.OfType<Picture>().ToList();
            Log.Information("Loading album '{AlbumName}' (Id={AlbumId}) with {Count} pictures...", album.Name, album.Id, pics.Count);

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
                if (pic.Parent == null) {
                    pic.Parent = album;
                }
            }
            pathService.PopulatePaths(firstBatchPics);

            // Load XMP metadata in parallel first
            await Task.WhenAll(firstBatchPics.Select(pic => xmpService.LoadMetadataAsync(pic)));

            // Sync picked and highlight files if they are missing
            foreach (var pic in firstBatchPics) {
                if (pic.CurationStatus == CurationStatus.Flagged) {
                    var pickedPath = pic.SubFolder?.Picked;
                    if (!string.IsNullOrEmpty(pickedPath) && !System.IO.File.Exists(pickedPath)) {
                        await pickedService.SyncToPickedAsync(pic);
                    }
                }
                if (pic.ColorLabel == ColorLabel.Blue) {
                    var highlightsPath = pathService.GetAlbumHighlightsPath(album);
                    if (!string.IsNullOrEmpty(highlightsPath)) {
                        var highlightFile = System.IO.Path.Combine(highlightsPath, pic.Name + ".jpg");
                        if (!System.IO.File.Exists(highlightFile)) {
                            var previewPath = pic.SubFolder?.Preview;
                            if (!string.IsNullOrEmpty(previewPath) && System.IO.File.Exists(previewPath)) {
                                var directory = System.IO.Path.GetDirectoryName(highlightFile);
                                if (directory != null && !System.IO.Directory.Exists(directory)) {
                                    System.IO.Directory.CreateDirectory(directory);
                                }
                                await Task.Run(() => System.IO.File.Copy(previewPath, highlightFile, true));
                            }
                        }
                    }
                }
            }

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
                if (FilterToolbar != null) {
                    FilterToolbar.UpdateAvailableTags(_allPictures);
                }
                PicturesList = new ObservableCollection<PictureItemViewModel>(filteredListInitial);
                GroupedPictures = new ObservableCollection<PictureGroupViewModel>(groupVmsInitial);

                if (hasPickedInitial) {
                    FilterToolbar?.SetFlaggedOnly();
                } else {
                    FilterToolbar?.ClearAll();
                }

                CanPlayCarousel = pics.Any();
                IsLoading = pics.Count > initialBatchSize;

                NotifyFilterStates();
            });

            if (pics.Count > initialBatchSize) {
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
                        if (pic.Parent == null) {
                            pic.Parent = album;
                        }
                    }
                    _pathService.PopulatePaths(chunk);

                    // Load XMP metadata in parallel background threads with capped concurrency
                    await Parallel.ForEachAsync(chunk, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (pic, token) => {
                        await _xmpService.LoadMetadataAsync(pic);
                    });

                    // Sync picked and highlight files if they are missing
                    foreach (var pic in chunk) {
                        if (pic.CurationStatus == CurationStatus.Flagged) {
                            var pickedPath = pic.SubFolder?.Picked;
                            if (!string.IsNullOrEmpty(pickedPath) && !System.IO.File.Exists(pickedPath)) {
                                await _pickedService.SyncToPickedAsync(pic);
                            }
                        }
                        if (pic.ColorLabel == ColorLabel.Blue) {
                            var highlightsPath = _pathService.GetAlbumHighlightsPath(album);
                            if (!string.IsNullOrEmpty(highlightsPath)) {
                                var highlightFile = System.IO.Path.Combine(highlightsPath, pic.Name + ".jpg");
                                if (!System.IO.File.Exists(highlightFile)) {
                                    var previewPath = pic.SubFolder?.Preview;
                                    if (!string.IsNullOrEmpty(previewPath) && System.IO.File.Exists(previewPath)) {
                                        var directory = System.IO.Path.GetDirectoryName(highlightFile);
                                        if (directory != null && !System.IO.Directory.Exists(directory)) {
                                            System.IO.Directory.CreateDirectory(directory);
                                        }
                                        await Task.Run(() => System.IO.File.Copy(previewPath, highlightFile, true));
                                    }
                                }
                            }
                        }
                    }

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
                        if (FilterToolbar != null) {
                            FilterToolbar.UpdateAvailableTags(_allPictures);
                        }

                        // Re-apply filters on chunk
                        var filteredChunkList = ApplyFilterPredicate(chunkVms).ToList();

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
            }

            // --- STAGE 3: Run Few-Shot Tag Discovery & Auto-Save Pipeline in background ---
            if (_tagDiscoveryService != null && _currentNode?.Id == album.Id) {
                try {
                    Log.Information("Starting Few-Shot Tag Discovery scan for {Count} picture(s) in album '{AlbumName}' (Id={AlbumId})...", pics.Count, album.Name, album.Id);
                    await _tagDiscoveryService.ScanPicturesAsync(pics, (pic, newKeywords) => {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            var vm = _allPictures.FirstOrDefault(p => p.Picture.Id == pic.Id);
                            if (vm != null) {
                                vm.Picture.Keywords = newKeywords;
                                vm.Keywords.Clear();
                                foreach (var kw in newKeywords) {
                                    vm.Keywords.Add(kw);
                                }
                                ApplyFilters();
                            }
                        });
                    }, cancellationToken);
                } catch (OperationCanceledException) {
                    // Album load or navigation was cancelled
                } catch (Exception ex) {
                    Log.Error(ex, "Error during automated few-shot tag discovery for album {AlbumId}", album.Id);
                }
            }
        });

        return Task.CompletedTask;
    }

    public ObservableCollection<BulkDeleteTagItemViewModel> AlbumTagsForBulkDelete { get; } = new();

    [RelayCommand]
    public void PopulateAlbumTagsForBulkDelete() {
        AlbumTagsForBulkDelete.Clear();
        if (_currentNode is not Album && !IsGlobalSearchActive) return;

        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var picVm in _allPictures) {
            if (picVm.Keywords != null && picVm.Keywords.Count > 0) {
                var chips = DetailsInspectorViewModel.DeduplicateAndFormatKeywords(picVm.Keywords);
                foreach (var chip in chips) {
                    var key = chip.DisplayText;
                    tagCounts[key] = tagCounts.GetValueOrDefault(key, 0) + 1;
                }
            }
        }

        foreach (var kvp in tagCounts.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)) {
            AlbumTagsForBulkDelete.Add(new BulkDeleteTagItemViewModel {
                TagName = kvp.Key,
                Count = kvp.Value,
                IsSelected = false
            });
        }
    }

    [RelayCommand]
    public async Task ExecuteBulkDeleteTagsAsync() {
        if (_currentNode is not Album && !IsGlobalSearchActive) return;

        var selectedTags = AlbumTagsForBulkDelete
            .Where(t => t.IsSelected && !string.IsNullOrWhiteSpace(t.TagName))
            .Select(t => t.TagName.Trim())
            .ToList();

        if (selectedTags.Count == 0) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("No Tag Selected")
                .WithContent("Please check at least one tag to delete.")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
            return;
        }

        int affectedFiles = 0;
        var affectedVms = new List<PictureItemViewModel>();

        foreach (var tag in selectedTags) {
            var normalizedTag = KeywordChipViewModel.NormalizePath(tag);
            foreach (var picVm in _allPictures) {
                var tagsToRemove = picVm.Keywords.Where(k => {
                    if (string.Equals(k, tag, StringComparison.OrdinalIgnoreCase)) return true;
                    var normK = KeywordChipViewModel.NormalizePath(k);
                    if (string.Equals(normK, normalizedTag, StringComparison.OrdinalIgnoreCase)) return true;
                    var segs = normK.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return segs.Any(s => string.Equals(s, tag.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(s, normalizedTag, StringComparison.OrdinalIgnoreCase));
                }).ToList();

                if (tagsToRemove.Count > 0) {
                    foreach (var tr in tagsToRemove) {
                        picVm.RemoveKeyword(tr);
                    }
                    _curationQueue.Enqueue(picVm.Picture);

                    if (!affectedVms.Contains(picVm)) {
                        affectedVms.Add(picVm);
                    }

                    if (_centroidService != null) {
                        var vec = picVm.Picture.Metrics?.GetEmbeddingVector();
                        if (vec != null) {
                            foreach (var tr in tagsToRemove) {
                                _centroidService.OnTagRemoved(picVm.Picture.Id, tr, vec);
                            }
                        }
                    }
                }
            }
        }

        affectedFiles = affectedVms.Count;

        if (FilterToolbar != null) {
            FilterToolbar.UpdateAvailableTags(_allPictures);
        }
        ApplyFilters();
        WeakReferenceMessenger.Default.Send(new PictureKeywordsChangedMessage(affectedVms));

        PopulateAlbumTagsForBulkDelete();

        var tagSummary = string.Join(", ", selectedTags);
        var albumName = (_currentNode as Album)?.Name ?? "Search Results";
        Log.Information("Bulk deleted tags [{Tags}] from {Count} files in '{AlbumName}'", tagSummary, affectedFiles, albumName);

        MainWindow.ToastManager.CreateToast()
            .WithTitle("Tags Deleted")
            .WithContent($"Removed {tagSummary} from {affectedFiles} picture(s).")
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();
        await Task.CompletedTask;
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
        _albumLoadCts?.Cancel();
        _albumLoadCts = null;
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
        if (XmpService.RecentWrites.TryGetValue(e.FullPath, out var writeTime)) {
            if (DateTime.UtcNow - writeTime < TimeSpan.FromSeconds(2)) {
                return;
            }
        }

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
                bool changed = picVm.CurationStatus != CurationStatus.Unflagged ||
                               picVm.ColorLabel != ColorLabel.None ||
                               picVm.Rating != 0 ||
                               picVm.Keywords.Any();

                picVm.Picture.CurationStatus = CurationStatus.Unflagged;
                picVm.Picture.ColorLabel = ColorLabel.None;
                picVm.Picture.Rating = 0;
                picVm.Picture.Keywords = new List<string>();

                picVm.CurationStatus = CurationStatus.Unflagged;
                picVm.ColorLabel = ColorLabel.None;
                picVm.Rating = 0;
                if (picVm.Keywords.Any()) {
                    picVm.Keywords.Clear();
                    picVm.NotifyKeywordsChanged();
                }

                if (changed) {
                    if (FilterToolbar != null) {
                        FilterToolbar.UpdateAvailableTags(_allPictures);
                    }
                    ApplyFilters();
                }
            } else {
                var originalStatus = picVm.Picture.CurationStatus;
                var originalColor = picVm.Picture.ColorLabel;
                var originalRating = picVm.Picture.Rating;
                var originalCapturedAt = picVm.Picture.CapturedAt;

                await _xmpService.LoadMetadataAsync(picVm.Picture);

                picVm.CurationStatus = picVm.Picture.CurationStatus;
                picVm.ColorLabel = picVm.Picture.ColorLabel;
                picVm.Rating = picVm.Picture.Rating;

                bool keywordsChanged = SyncKeywords(picVm);

                bool changed = picVm.Picture.CurationStatus != originalStatus ||
                               picVm.Picture.ColorLabel != originalColor ||
                               picVm.Picture.Rating != originalRating ||
                               picVm.Picture.CapturedAt != originalCapturedAt;

                if (picVm.Picture.CapturedAt != originalCapturedAt) {
                    await _nodeService.UpdateNodeAsync(picVm.Picture);
                    _ = RefreshGalleryGrouping();
                }

                if (changed || keywordsChanged) {
                    if (FilterToolbar != null) {
                        FilterToolbar.UpdateAvailableTags(_allPictures);
                    }
                    ApplyFilters();
                }
            }
        });
    }

    private void OnXmpFileRenamed(object sender, RenamedEventArgs e) {
        if (XmpService.RecentWrites.TryGetValue(e.FullPath, out var writeTime)) {
            if (DateTime.UtcNow - writeTime < TimeSpan.FromSeconds(2)) {
                return;
            }
        }

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

            bool changed = false;

            if (!string.IsNullOrEmpty(oldName)) {
                var oldPicVm = _allPictures.FirstOrDefault(p => p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
                if (oldPicVm != null) {
                    if (oldPicVm.CurationStatus != CurationStatus.Unflagged ||
                        oldPicVm.ColorLabel != ColorLabel.None ||
                        oldPicVm.Rating != 0 ||
                        oldPicVm.Keywords.Any()) {
                        changed = true;
                    }

                    oldPicVm.Picture.CurationStatus = CurationStatus.Unflagged;
                    oldPicVm.Picture.ColorLabel = ColorLabel.None;
                    oldPicVm.Picture.Rating = 0;
                    oldPicVm.Picture.Keywords = new List<string>();

                    oldPicVm.CurationStatus = CurationStatus.Unflagged;
                    oldPicVm.ColorLabel = ColorLabel.None;
                    oldPicVm.Rating = 0;
                    if (oldPicVm.Keywords.Any()) {
                        oldPicVm.Keywords.Clear();
                        oldPicVm.NotifyKeywordsChanged();
                    }
                }
            }

            if (!string.IsNullOrEmpty(newName)) {
                var newPicVm = _allPictures.FirstOrDefault(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                if (newPicVm != null) {
                    var originalStatus = newPicVm.Picture.CurationStatus;
                    var originalColor = newPicVm.Picture.ColorLabel;
                    var originalRating = newPicVm.Picture.Rating;
                    var originalCapturedAt = newPicVm.Picture.CapturedAt;

                    await _xmpService.LoadMetadataAsync(newPicVm.Picture);

                    newPicVm.CurationStatus = newPicVm.Picture.CurationStatus;
                    newPicVm.ColorLabel = newPicVm.Picture.ColorLabel;
                    newPicVm.Rating = newPicVm.Picture.Rating;

                    bool keywordsChanged = SyncKeywords(newPicVm);

                    if (newPicVm.Picture.CurationStatus != originalStatus ||
                        newPicVm.Picture.ColorLabel != originalColor ||
                        newPicVm.Picture.Rating != originalRating ||
                        newPicVm.Picture.CapturedAt != originalCapturedAt ||
                        keywordsChanged) {
                        changed = true;
                    }

                    if (newPicVm.Picture.CapturedAt != originalCapturedAt) {
                        await _nodeService.UpdateNodeAsync(newPicVm.Picture);
                        _ = RefreshGalleryGrouping();
                    }
                }
            }

            if (changed) {
                if (FilterToolbar != null) {
                    FilterToolbar.UpdateAvailableTags(_allPictures);
                }
                ApplyFilters();
            }
        });
    }

    private bool SyncKeywords(PictureItemViewModel picVm) {
        var originalKeywords = picVm.Keywords.ToList();
        var newKeywords = picVm.Picture.Keywords ?? new List<string>();
        bool keywordsChanged = !originalKeywords.SequenceEqual(newKeywords);
        if (keywordsChanged) {
            picVm.Keywords.Clear();
            foreach (var kw in newKeywords) {
                picVm.Keywords.Add(kw);
            }
            picVm.NotifyKeywordsChanged();
        }
        return keywordsChanged;
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
