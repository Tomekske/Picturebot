using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.Interfaces;
using PictureWorker.Domain.Interfaces;
using Picturebot.Messages;
using Picturebot.Utilities;
using Serilog;

using Picturebot.Views;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public record ColorLabelOption(ColorLabel Label, string Name, string HexColor);

public partial class QuickTagButtonViewModel : ObservableObject {
    [ObservableProperty]
    private bool _isActive;

    public Tag Tag { get; set; } = new();
    public string Name => Tag.Name;
}

public partial class DetailsInspectorViewModel : ViewModelBase, IRecipient<PictureSelectedMessage>, IRecipient<PictureSelectionChangedMessage> {
    private readonly INodeService _nodeService;
    private readonly ICurationQueue _curationQueue;
    private readonly ISettingsService _settingsService;
    private readonly IAlbumService _albumService;
    private readonly IGlobalExemplarCentroidService? _centroidService;
    private readonly IImageEmbeddingService? _embeddingService;
    private readonly ITaxonomyService? _taxonomyService;
    private readonly IFewShotTagDiscoveryService? _tagDiscoveryService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private PictureItemViewModel? _selectedPicture;

    [ObservableProperty]
    private ColorLabelOption? _selectedColorLabelOption;

    [ObservableProperty]
    private ObservableCollection<PictureItemViewModel> _selectedPictures = new();

    [ObservableProperty]
    private string _newTagText = string.Empty;

    public ObservableCollection<KeywordChipViewModel> ActiveKeywordChips { get; } = new();

    public ObservableCollection<string> ActiveKeywords { get; } = new();

    public ObservableCollection<string> AvailableKeywordSuggestions { get; } = new();

    public ObservableCollection<TagGroup> AvailableTagGroups { get; } = new();

    [ObservableProperty]
    private TagGroup? _activeTagGroup;

    public ObservableCollection<QuickTagButtonViewModel> QuickTagButtons { get; } = new();

    public DetailsInspectorViewModel(
        INodeService nodeService,
        ICurationQueue curationQueue,
        ISettingsService settingsService,
        IAlbumService albumService,
        IGlobalExemplarCentroidService? centroidService = null,
        IImageEmbeddingService? embeddingService = null,
        ITaxonomyService? taxonomyService = null,
        IFewShotTagDiscoveryService? tagDiscoveryService = null) {
        _nodeService = nodeService;
        _curationQueue = curationQueue;
        _settingsService = settingsService;
        _albumService = albumService;
        _centroidService = centroidService;
        _embeddingService = embeddingService;
        _taxonomyService = taxonomyService;
        _tagDiscoveryService = tagDiscoveryService;
        _settingsService.PropertyChanged += OnSettingsChanged;
        WeakReferenceMessenger.Default.RegisterAll(this);
        RefreshTagGroups();
        RefreshKeywordSuggestions();
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(ISettingsService.Current)) {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshTagGroups();
                RefreshKeywordSuggestions();
            });
        }
    }

    partial void OnActiveTagGroupChanged(TagGroup? value) {
        BuildQuickTagButtons();
    }

    public string RedLabelName => _settingsService.Current.RedLabelName;
    public string OrangeLabelName => _settingsService.Current.OrangeLabelName;
    public string YellowLabelName => _settingsService.Current.YellowLabelName;
    public string GreenLabelName => _settingsService.Current.GreenLabelName;
    public string BlueLabelName => _settingsService.Current.BlueLabelName;
    public string PinkLabelName => _settingsService.Current.PinkLabelName;
    public string PurpleLabelName => _settingsService.Current.PurpleLabelName;

    public List<ColorLabelOption> ColorLabelOptions => new() {
        new(ColorLabel.None, "None", "Transparent"),
        new(ColorLabel.Red, RedLabelName, "#B71C1C"),
        new(ColorLabel.Orange, OrangeLabelName, "#E67E22"),
        new(ColorLabel.Yellow, YellowLabelName, "#FDD835"),
        new(ColorLabel.Green, GreenLabelName, "#33CC33"),
        new(ColorLabel.Blue, BlueLabelName, "#3333CC"),
        new(ColorLabel.Pink, PinkLabelName, "#F06292"),
        new(ColorLabel.Purple, PurpleLabelName, "#CC33CC")
    };

    public ActiveMode ActiveMode => SelectedPictures.Count >= 2 ? ActiveMode.MultiMode : ActiveMode.SingleMode;
    public bool IsMultiMode => SelectedPictures.Count >= 2;
    public bool IsSingleMode => SelectedPictures.Count <= 1;

    public void Receive(PictureSelectedMessage message) {
        SelectedPicture = message.Value;
    }

    private bool _isUpdatingFromSelectionChanged;

    public void Receive(PictureSelectionChangedMessage message) {
        _isUpdatingFromSelectionChanged = true;
        try {
            SelectedPictures.Clear();
            foreach (var pic in message.Value) {
                SelectedPictures.Add(pic);
            }
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            OnPropertyChanged(nameof(ActiveMode));
            OnPropertyChanged(nameof(IsMultiMode));
            OnPropertyChanged(nameof(IsSingleMode));
        } finally {
            _isUpdatingFromSelectionChanged = false;
        }
    }

    private PictureItemViewModel? _activePicture;

    async partial void OnSelectedPictureChanged(PictureItemViewModel? value) {
        if (_activePicture != null) {
            _activePicture.PropertyChanged -= OnPicturePropertyChanged;
        }

        _activePicture = value;

        PreviewImage?.Dispose();
        PreviewImage = null;

        if (value == null) {
            SelectedColorLabelOption = null;
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            return;
        }

        value.PropertyChanged += OnPicturePropertyChanged;

        SelectedColorLabelOption = ColorLabelOptions.FirstOrDefault(o => o.Label == value.ColorLabel);
        UpdateActiveKeywords();
        UpdateQuickTagStates();
        await LoadPreviewAsync(value);
    }

    private void OnPicturePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(PictureItemViewModel.ColorLabel) && SelectedPicture != null) {
            var newOption = ColorLabelOptions.FirstOrDefault(o => o.Label == SelectedPicture.ColorLabel);
            if (SelectedColorLabelOption != newOption) {
                SelectedColorLabelOption = newOption;
            }
        }
        if (e.PropertyName == nameof(PictureItemViewModel.Keywords)) {
            UpdateActiveKeywords();
            UpdateQuickTagStates();
        }
    }

    private void UpdateActiveKeywords() {
        ActiveKeywordChips.Clear();
        ActiveKeywords.Clear();

        HashSet<string> rawKeywords;
        if (SelectedPictures.Count >= 2) {
            rawKeywords = SelectedPictures.SelectMany(p => p.Keywords).ToHashSet(StringComparer.OrdinalIgnoreCase);
        } else if (SelectedPictures.Count == 1) {
            rawKeywords = SelectedPictures[0].Keywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        } else if (SelectedPicture != null) {
            rawKeywords = SelectedPicture.Keywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        } else {
            rawKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var chips = DeduplicateAndFormatKeywords(rawKeywords);
        foreach (var chip in chips) {
            ActiveKeywordChips.Add(chip);
            ActiveKeywords.Add(chip.DisplayText);
        }
    }

    public static List<KeywordChipViewModel> DeduplicateAndFormatKeywords(IEnumerable<string> rawKeywords) {
        if (rawKeywords == null) return new List<KeywordChipViewModel>();

        var rawList = rawKeywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hierarchicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var flatTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawList) {
            var normalized = KeywordChipViewModel.NormalizePath(raw);
            if (normalized.Contains('|')) {
                hierarchicalPaths.Add(normalized);
            } else {
                flatTags.Add(normalized);
            }
        }

        // 1. Remove subsumed / prefix hierarchical paths (keep longest canonical paths)
        var canonicalHierarchies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in hierarchicalPaths) {
            bool isSubsumed = hierarchicalPaths.Any(other =>
                other.Length > path.Length &&
                other.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase));
            if (!isSubsumed) {
                canonicalHierarchies.Add(path);
            }
        }

        // 2. Identify all absorbed ancestor segments and leaf segments
        var absorbedSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in canonicalHierarchies) {
            var segments = path.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var seg in segments) {
                absorbedSegments.Add(seg);
            }
        }

        var chips = new List<KeywordChipViewModel>();

        // 3. Add canonical hierarchical chips
        foreach (var path in canonicalHierarchies) {
            chips.Add(KeywordChipViewModel.FromHierarchicalPath(path));
        }

        // 4. Add standalone flat tags (those not absorbed by hierarchical paths)
        foreach (var flat in flatTags) {
            if (!absorbedSegments.Contains(flat)) {
                chips.Add(KeywordChipViewModel.FromFlatTag(flat));
            }
        }

        return chips.OrderBy(c => c.DisplayText, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void RefreshKeywordSuggestions() {
        AvailableKeywordSuggestions.Clear();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Hierarchy paths from settings (formatted as breadcrumbs)
        var hierarchy = _settingsService.Current?.HierarchyNodes;
        if (hierarchy != null) {
            foreach (var root in hierarchy) {
                CollectHierarchyPathsForSuggestions(root, "", set);
            }
        }

        // 2. Master tags
        var masterTags = _settingsService.Current?.MasterTags;
        if (masterTags != null) {
            foreach (var t in masterTags) {
                if (!string.IsNullOrWhiteSpace(t.Name)) {
                    set.Add(t.Name.Trim());
                }
            }
        }

        // 3. Existing keywords from pictures
        foreach (var pic in SelectedPictures) {
            foreach (var kw in pic.Keywords) {
                if (kw.Contains('|')) {
                    set.Add(kw.Replace("|", " › "));
                } else {
                    set.Add(kw);
                }
            }
        }

        foreach (var item in set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)) {
            AvailableKeywordSuggestions.Add(item);
        }
    }

    private static void CollectHierarchyPathsForSuggestions(HierarchyNode node, string parentPath, HashSet<string> set) {
        var path = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath} › {node.Name}";
        set.Add(path);
        if (node.Children != null) {
            foreach (var child in node.Children) {
                CollectHierarchyPathsForSuggestions(child, path, set);
            }
        }
    }

    private void RefreshTagGroups() {
        AvailableTagGroups.Clear();
        var groups = _settingsService.Current.TagGroups;
        foreach (var g in groups) {
            AvailableTagGroups.Add(g);
        }

        var activeId = _settingsService.Current.ActiveTagGroupId;
        ActiveTagGroup = AvailableTagGroups.FirstOrDefault(g => g.GroupId == activeId) ?? AvailableTagGroups.FirstOrDefault();
    }

    private void BuildQuickTagButtons() {
        QuickTagButtons.Clear();
        if (ActiveTagGroup == null) return;

        var masterTags = _settingsService.Current.MasterTags.ToDictionary(t => t.Id);
        var activeTags = GetActiveTags();

        foreach (var tagId in ActiveTagGroup.TagIds) {
            if (masterTags.TryGetValue(tagId, out var tag)) {
                var paths = GetKeywordPathsForTag(tag);
                bool isActive = paths.Any(p => activeTags.Contains(p) || activeTags.Contains(tag.Name));
                QuickTagButtons.Add(new QuickTagButtonViewModel { Tag = tag, IsActive = isActive });
            }
        }
    }

    private void UpdateQuickTagStates() {
        var activeTags = GetActiveTags();
        foreach (var btn in QuickTagButtons) {
            var paths = GetKeywordPathsForTag(btn.Tag);
            btn.IsActive = paths.Any(p => activeTags.Contains(p) || activeTags.Contains(btn.Tag.Name));
        }
    }

    private HashSet<string> GetActiveTags() {
        if (SelectedPictures.Any())
            return SelectedPictures.SelectMany(p => p.Keywords).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (SelectedPicture != null)
            return SelectedPicture.Keywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private List<string> GetKeywordPathsForTag(Tag tag) {
        var paths = new List<string>();
        var hierarchy = _settingsService.Current.HierarchyNodes;
        var linkedNodes = FindLinkedHierarchyNodes(hierarchy, tag.Id);
        if (linkedNodes.Any()) {
            foreach (var node in linkedNodes) {
                var path = FindNodePath(hierarchy, node, "");
                if (!string.IsNullOrEmpty(path)) {
                    paths.Add(path);
                }
            }
        }
        if (!paths.Any()) {
            paths.Add(tag.Name);
        }
        return paths;
    }

    private static List<HierarchyNode> FindLinkedHierarchyNodes(IEnumerable<HierarchyNode> nodes, Guid tagId) {
        var list = new List<HierarchyNode>();
        foreach (var node in nodes) {
            if (node.TagId == tagId) list.Add(node);
            list.AddRange(FindLinkedHierarchyNodes(node.Children, tagId));
        }
        return list;
    }

    private static string? FindNodePath(IEnumerable<HierarchyNode> nodes, HierarchyNode target, string currentPath) {
        foreach (var node in nodes) {
            var path = string.IsNullOrEmpty(currentPath) ? node.Name : $"{currentPath}|{node.Name}";
            if (node == target) return path;
            var childResult = FindNodePath(node.Children, target, path);
            if (childResult != null) return childResult;
        }
        return null;
    }

    partial void OnNewTagTextChanged(string value) {
        if (value != null && value.EndsWith(",")) {
            var tag = value.TrimEnd(',');
            if (!string.IsNullOrWhiteSpace(tag)) {
                AddKeyword(tag);
            }
            NewTagText = string.Empty;
        }
    }

    partial void OnSelectedColorLabelOptionChanged(ColorLabelOption? value) {
        if (value != null && SelectedPicture != null && SelectedPicture.ColorLabel != value.Label) {
            _ = SetColorLabel(value.Label);
        }
    }

    private async Task LoadPreviewAsync(PictureItemViewModel picVm) {
        var previewPath = picVm.Picture.SubFolder?.Preview;
        if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath)) {
            Log.Warning("No preview available for {Name}", picVm.Name);
            PreviewImage?.Dispose();
            PreviewImage = null;
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try {
            var loadedBitmap = await ImageHelper.LoadAndOrientAsync(previewPath, 600);
            if (token.IsCancellationRequested || SelectedPicture != picVm) {
                loadedBitmap?.Dispose();
                return;
            }
            PreviewImage?.Dispose();
            PreviewImage = loadedBitmap;
        } catch (OperationCanceledException) {
            // Loading was cancelled
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load preview for {Name} at {Path}", picVm.Name, previewPath);
        }
    }

    [RelayCommand]
    private async Task SetCurationStatus(CurationStatus status) {
        var pictureVm = SelectedPicture;
        if (pictureVm == null) {
            return;
        }

        try {
            pictureVm.CurationStatus = status;
            _curationQueue.Enqueue(pictureVm.Picture);
            await Task.CompletedTask;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update curation status for {Name}", pictureVm.Name);
        }
    }

    [RelayCommand]
    private async Task SetColorLabel(ColorLabel label) {
        var pictureVm = SelectedPicture;
        if (pictureVm == null) {
            return;
        }

        try {
            pictureVm.ColorLabel = label;
            _curationQueue.Enqueue(pictureVm.Picture);
            await Task.CompletedTask;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update color label for {Name}", pictureVm.Name);
        }
    }

    [RelayCommand]
    private async Task SetRating(string ratingStr) {
        var pictureVm = SelectedPicture;
        if (pictureVm == null || !int.TryParse(ratingStr, out var rating)) {
            return;
        }

        try {
            pictureVm.Rating = rating;
            _curationQueue.Enqueue(pictureVm.Picture);
            await Task.CompletedTask;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update rating for {Name}", pictureVm.Name);
        }
    }

    [RelayCommand]
    private void EditMetadata() {
        Log.Information("Edit metadata for {Name}", SelectedPicture?.Name);
    }

    [RelayCommand]
    private void DeleteAsset() {
        if (SelectedPicture == null || SelectedPicture.Picture == null) return;

        var pictureToDelete = SelectedPicture.Picture;
        var pictureName = SelectedPicture.Name;
        var title = "Delete Picture";
        var message = $"Are you sure you want to delete picture '{pictureName}'? This will move its JPG and RAW files to the 'Deleted' folder and remove it from the library.";

        var vm = new ConfirmDeleteDialogViewModel(title, message, async result => {
            if (result) {
                try {
                    await _albumService.DeletePictureAsync(pictureToDelete);
                    Log.Information("Picture deleted: {Name}", pictureName);

                    MainWindow.ToastManager.CreateToast()
                        .WithTitle("Success")
                        .WithContent($"Picture '{pictureName}' has been deleted.")
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Queue();

                    SelectedPicture = null;
                    WeakReferenceMessenger.Default.Send(new NodeDeletedMessage(pictureToDelete));
                } catch (Exception ex) {
                    Log.Error(ex, "Failed to delete picture {Name}", pictureName);
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

    [RelayCommand]
    public void AddKeyword(string keyword) {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        var input = keyword.Trim();

        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        string? resolvedHierarchicalPath = null;
        List<string> flatSegmentsToAdd = new();

        var normalized = KeywordChipViewModel.NormalizePath(input);
        if (normalized.Contains('|')) {
            resolvedHierarchicalPath = normalized;
            flatSegmentsToAdd = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        } else {
            // Check if input is a leaf tag in taxonomy service
            if (_taxonomyService != null) {
                var fullHierarchy = _taxonomyService.GetFullHierarchicalPath(input);
                if (!string.IsNullOrEmpty(fullHierarchy) && fullHierarchy.Contains('|')) {
                    resolvedHierarchicalPath = fullHierarchy;
                    flatSegmentsToAdd = _taxonomyService.ResolveTaxonomySubjectChain(input).ToList();
                }
            }
            if (resolvedHierarchicalPath == null) {
                var pathFromHierarchy = FindHierarchicalPathForTagInSettings(input);
                if (!string.IsNullOrEmpty(pathFromHierarchy) && pathFromHierarchy.Contains('|')) {
                    resolvedHierarchicalPath = pathFromHierarchy;
                    flatSegmentsToAdd = pathFromHierarchy.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                } else {
                    flatSegmentsToAdd.Add(input);
                }
            }
        }

        bool changed = false;
        foreach (var picVm in targetVms) {
            if (!string.IsNullOrEmpty(resolvedHierarchicalPath)) {
                if (!picVm.Keywords.Contains(resolvedHierarchicalPath, StringComparer.OrdinalIgnoreCase)) {
                    picVm.AddKeyword(resolvedHierarchicalPath);
                    changed = true;
                }
            }

            foreach (var seg in flatSegmentsToAdd) {
                if (!picVm.Keywords.Contains(seg, StringComparer.OrdinalIgnoreCase)) {
                    picVm.AddKeyword(seg);
                    changed = true;
                }
            }

            if (changed) {
                _curationQueue.Enqueue(picVm.Picture);
                if (_centroidService != null && _embeddingService != null) {
                    _ = Task.Run(async () => {
                        var vec = await _embeddingService.GetOrComputeEmbeddingAsync(picVm.Picture);
                        _centroidService.OnTagAdded(picVm.Picture.Id, resolvedHierarchicalPath ?? input, vec);
                    });
                }
            }
        }

        if (changed) {
            Log.Information("Manually added tag '{Keyword}' to {Count} picture(s): [{PictureNames}]",
                input, targetVms.Count, string.Join(", ", targetVms.Select(p => p.Name)));
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            WeakReferenceMessenger.Default.Send(new PictureKeywordsChangedMessage(targetVms));
        }
    }

    [RelayCommand]
    public void RemoveKeywordChip(KeywordChipViewModel? chip) {
        if (chip == null) return;
        RemoveKeyword(chip.RawValue);
    }

    [RelayCommand]
    public void RemoveKeyword(string keyword) {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        var normalized = KeywordChipViewModel.NormalizePath(keyword);

        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        bool changed = false;
        foreach (var picVm in targetVms) {
            if (normalized.Contains('|')) {
                // 1. Remove the matching hierarchical path(s)
                var matchingPaths = picVm.Keywords
                    .Where(k => KeywordChipViewModel.NormalizePath(k).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var mp in matchingPaths) {
                    picVm.RemoveKeyword(mp);
                    changed = true;
                }

                // 2. Remove associated segments if they are not used in any remaining hierarchical path
                var segmentsToRemove = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var remainingHierarchical = picVm.Keywords
                    .Select(KeywordChipViewModel.NormalizePath)
                    .Where(k => k.Contains('|'))
                    .ToList();

                foreach (var seg in segmentsToRemove) {
                    bool usedInOtherHierarchy = remainingHierarchical.Any(other =>
                        other.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                             .Any(s => s.Equals(seg, StringComparison.OrdinalIgnoreCase)));

                    if (!usedInOtherHierarchy) {
                        var flatMatch = picVm.Keywords.FirstOrDefault(k => k.Equals(seg, StringComparison.OrdinalIgnoreCase));
                        if (flatMatch != null) {
                            picVm.RemoveKeyword(flatMatch);
                            changed = true;
                        }
                    }
                }
            } else {
                // Flat tag removal
                var existing = picVm.Keywords.FirstOrDefault(k =>
                    k.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                    k.Equals(keyword.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existing != null) {
                    picVm.RemoveKeyword(existing);
                    changed = true;
                }
            }

            if (changed) {
                _curationQueue.Enqueue(picVm.Picture);
                if (_centroidService != null && _embeddingService != null) {
                    _ = Task.Run(async () => {
                        var vec = await _embeddingService.GetOrComputeEmbeddingAsync(picVm.Picture);
                        _centroidService.OnTagRemoved(picVm.Picture.Id, normalized, vec);
                    });
                }
            }
        }

        if (changed) {
            Log.Information("Manually removed tag '{Keyword}' from {Count} picture(s): [{PictureNames}]",
                keyword, targetVms.Count, string.Join(", ", targetVms.Select(p => p.Name)));
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            WeakReferenceMessenger.Default.Send(new PictureKeywordsChangedMessage(targetVms));
        }
    }

    [RelayCommand]
    private void CommitNewKeyword() {
        if (!string.IsNullOrWhiteSpace(NewTagText)) {
            AddKeyword(NewTagText);
            NewTagText = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleQuickTag(QuickTagButtonViewModel? buttonVm) {
        if (buttonVm == null) return;
        var tag = buttonVm.Tag;
        var paths = GetKeywordPathsForTag(tag);

        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        bool changed = false;
        foreach (var picVm in targetVms) {
            bool hasAny = paths.Any(p => picVm.Keywords.Contains(p, StringComparer.OrdinalIgnoreCase));
            if (hasAny) {
                foreach (var p in paths) {
                    RemoveKeyword(p);
                }
            } else {
                foreach (var p in paths) {
                    AddKeyword(p);
                }
            }
            changed = true;
        }

        if (changed) {
            Log.Information("Toggled quick tag '{TagName}' on {Count} picture(s): [{PictureNames}]",
                tag.Name, targetVms.Count, string.Join(", ", targetVms.Select(p => p.Name)));
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            WeakReferenceMessenger.Default.Send(new PictureKeywordsChangedMessage(targetVms));
        }
    }

    private string? FindHierarchicalPathForTagInSettings(string leafTagName) {
        var hierarchy = _settingsService.Current?.HierarchyNodes;
        if (hierarchy == null) return null;
        return FindPathToNodeByName(hierarchy, leafTagName, "");
    }

    private static string? FindPathToNodeByName(IEnumerable<HierarchyNode> nodes, string targetName, string currentPath) {
        foreach (var node in nodes) {
            var path = string.IsNullOrEmpty(currentPath) ? node.Name : $"{currentPath}|{node.Name}";
            if (node.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase)) {
                return path;
            }
            var child = FindPathToNodeByName(node.Children, targetName, path);
            if (child != null) return child;
        }
        return null;
    }

    [RelayCommand]
    private async Task AutoTagPictureAsync() {
        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        if (!targetVms.Any()) {
            Log.Warning("AI Auto-Tag: No picture selected.");
            return;
        }

        if (_centroidService == null || _embeddingService == null) {
            Log.Warning("AI Auto-Tag: AI Centroid or Embedding services not available.");
            return;
        }

        IsBusy = true;
        try {
            Log.Information("AI Auto-Tag: Checking tags for {Count} picture(s)...", targetVms.Count);
            var centroids = await _centroidService.GetActiveLeafCentroidsAsync();

            if (centroids == null || centroids.Count == 0) {
                Log.Warning("AI Auto-Tag: No active leaf centroids found in library (ensure tags have exemplars).");
                return;
            }

            int changedCount = 0;
            foreach (var picVm in targetVms) {
                var pic = picVm.Picture;
                var embedding = await _embeddingService.GetOrComputeEmbeddingAsync(pic);
                if (embedding == null || embedding.Length != 512) {
                    Log.Warning("AI Auto-Tag: Could not extract visual embedding for picture '{Name}'", pic.Name);
                    continue;
                }

                // Match against active centroids
                var candidateScores = new List<(string LeafTag, float Similarity)>();
                foreach (var (leafTag, centroid) in centroids) {
                    float dot = 0.0f;
                    for (int i = 0; i < 512; i++) {
                        dot += embedding[i] * centroid[i];
                    }
                    candidateScores.Add((leafTag, dot));
                }

                if (candidateScores.Count == 0) continue;

                const float threshold = 0.70f;
                float maxScore = candidateScores.Max(c => c.Similarity);
                var winningTags = candidateScores
                    .Where(c => c.Similarity >= maxScore - 0.05f && c.Similarity >= threshold)
                    .Select(c => c.LeafTag)
                    .ToList();

                if (winningTags.Count == 0) {
                    Log.Information("AI Auto-Tag: No confident match for picture '{Name}' above {Threshold:P0} threshold (Best score: {Score:P1})", pic.Name, threshold, maxScore);
                    continue;
                }

                var newKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var leaf in winningTags) {
                    if (_taxonomyService != null) {
                        var flatChain = _taxonomyService.ResolveTaxonomySubjectChain(leaf);
                        var fullHierarchy = _taxonomyService.GetFullHierarchicalPath(leaf);
                        foreach (var f in flatChain) newKeywords.Add(f);
                        if (!string.IsNullOrEmpty(fullHierarchy)) newKeywords.Add(fullHierarchy);
                    } else {
                        newKeywords.Add(leaf);
                    }
                }

                // OVERRIDE existing keywords
                picVm.Keywords.Clear();
                foreach (var kw in newKeywords) {
                    picVm.Keywords.Add(kw);
                }
                pic.Keywords = newKeywords.ToList();
                pic.KeywordsJson = System.Text.Json.JsonSerializer.Serialize(pic.Keywords);

                _curationQueue.Enqueue(pic);
                changedCount++;

                Log.Information("AI Auto-Tag: Successfully overrode tags on '{Name}' with [{Tags}] (Score: {Score:P1})",
                    pic.Name, string.Join(", ", newKeywords), maxScore);
            }

            if (changedCount > 0) {
                UpdateActiveKeywords();
                UpdateQuickTagStates();
                WeakReferenceMessenger.Default.Send(new PictureKeywordsChangedMessage(targetVms));
            }
        } catch (Exception ex) {
            Log.Error(ex, "Error executing AI Auto-Tag");
        } finally {
            IsBusy = false;
        }
    }
}
