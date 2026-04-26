using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class GalleryViewModel : ViewModelBase, IRecipient<NodeSelectedMessage>, IRecipient<NodeCreatedMessage> {
    private readonly IPictureGroupingService _groupingService;
    private readonly INavigationService _navigationService;
    private readonly INodeService _nodeService;
    private readonly IPathService _pathService;
    private readonly ISettingsService _settingsService;

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
    private ObservableCollection<Node> _items = new();

    [ObservableProperty]
    private ObservableCollection<PictureItemViewModel> _picturesList = new();

    [ObservableProperty]
    private PictureItemViewModel? _selectedPicture;

    public GalleryViewModel(INodeService nodeService, IPathService pathService,
        IPictureGroupingService groupingService, INavigationService navigationService,
        ISettingsService settingsService) {
        _nodeService = nodeService;
        _pathService = pathService;
        _groupingService = groupingService;
        _navigationService = navigationService;
        _settingsService = settingsService;
        WeakReferenceMessenger.Default.RegisterAll(this);
        _ = LoadInitialItemsAsync();
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

            // Persist to database (sequentially for stability)
            await _nodeService.UpdateNodeAsync(picVm.Picture);
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
            .OrderByDescending(g => g.Key);

        foreach (var group in groups) {
            var dateStr = group.Key.ToString("yyyy-MM-dd");
            var count = group.Count();
            var header = $"{dateStr} ({count})";
            var sortedGroup = group.OrderBy(p => p.Picture.CapturedAt);
            var groupVm = new PictureGroupViewModel(dateStr, header,
                new ObservableCollection<PictureItemViewModel>(sortedGroup));
            GroupedPictures.Add(groupVm);
        }
    }

    private async Task ApplyBurstGrouping() {
        if (_currentNode == null) {
            return;
        }

        // Threshold 6 for > 90% similarity
        var groups = await _groupingService.GroupSimilarPicturesAsync(_currentNode.Id, 6);

        if (groups.Count == 0) {
            ApplyDateGrouping();
            return;
        }

        var groupIndex = 1;
        var groupedIds = new HashSet<int>();

        foreach (var group in groups) {
            if (group.Count <= 1) {
                continue;
            }

            var picVms = group.Select(p => {
                    var vm = PicturesList.FirstOrDefault(vm => vm.Picture.Id == p.Id);
                    if (vm != null) {
                        groupedIds.Add(p.Id);
                    }

                    return vm;
                }).Where(vm => vm != null).Cast<PictureItemViewModel>()
                .OrderBy(vm => vm.Picture.CapturedAt)
                .ToList();

            if (picVms.Count == 0) {
                continue;
            }

            var bestPic = picVms.OrderByDescending(vm => vm.Picture.Sharpness).FirstOrDefault();
            if (bestPic != null) {
                bestPic.IsBest = true;
            }

            var header = $"Burst Group {groupIndex++} ({picVms.Count})";
            var groupVm = new PictureGroupViewModel(header, header,
                new ObservableCollection<PictureItemViewModel>(picVms), true);
            GroupedPictures.Add(groupVm);
        }

        var unclassified = PicturesList.Where(vm => !groupedIds.Contains(vm.Picture.Id))
            .OrderBy(vm => vm.Picture.CapturedAt)
            .ToList();
        if (unclassified.Count > 0) {
            var header = $"Unclassified ({unclassified.Count})";
            var groupVm = new PictureGroupViewModel("Unclassified", header,
                new ObservableCollection<PictureItemViewModel>(unclassified));
            GroupedPictures.Add(groupVm);
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
                .Queue();
            return;
        }

        var albumPath = Path.Combine(libraryPath, album.Uuid);

        if (!Directory.Exists(albumPath)) {
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent("Album directory does not exist or is inaccessible.")
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
                .Queue();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlayCarousel))]
    private void PlayCarousel() {
        var window = new CarouselWindow();
        var carouselVm = new CarouselDialogViewModel(PicturesList, SelectedPicture, _nodeService, window.Close);
        window.DataContext = carouselVm;

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

        if (value != null) {
            WeakReferenceMessenger.Default.Send(new PictureSelectedMessage(value));
        }
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
                    .OrderByDescending(p => p.CapturedAt)
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
}

public class BreadcrumbItem(string name, Node? node) {
    public string Name { get; } = name;
    public Node? Node { get; } = node;
    public bool IsLast { get; set; }
}
