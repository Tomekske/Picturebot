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
using Graph.Domain.Interfaces;
using Picturebot.Messages;
using Picturebot.Utilities;
using Serilog;

namespace Picturebot.ViewModels;

public record ColorLabelOption(ColorLabel Label, string Name, string HexColor);

public partial class DetailsInspectorViewModel : ViewModelBase, IRecipient<PictureSelectedMessage>, IRecipient<PictureSelectionChangedMessage> {
    private readonly INodeService _nodeService;
    private readonly ICurationQueue _curationQueue;
    private readonly ISettingsService _settingsService;
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

    public ObservableCollection<string> ActiveKeywords { get; } = new();

    public List<string> QuickTags { get; } = new() { "Selected", "Review", "Highlight", "Portrait", "Landscape" };

    [ObservableProperty]
    private bool _isQuickTag1Active;
    [ObservableProperty]
    private bool _isQuickTag2Active;
    [ObservableProperty]
    private bool _isQuickTag3Active;
    [ObservableProperty]
    private bool _isQuickTag4Active;
    [ObservableProperty]
    private bool _isQuickTag5Active;

    public DetailsInspectorViewModel(INodeService nodeService, ICurationQueue curationQueue, ISettingsService settingsService) {
        _nodeService = nodeService;
        _curationQueue = curationQueue;
        _settingsService = settingsService;
        WeakReferenceMessenger.Default.RegisterAll(this);
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

    public void Receive(PictureSelectedMessage message) {
        SelectedPicture = message.Value;
    }

    public void Receive(PictureSelectionChangedMessage message) {
        SelectedPictures.Clear();
        foreach (var pic in message.Value) {
            SelectedPictures.Add(pic);
        }
        UpdateActiveKeywords();
        UpdateQuickTagStates();
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
            SelectedPictures.Clear();
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            return;
        }

        value.PropertyChanged += OnPicturePropertyChanged;

        SelectedColorLabelOption = ColorLabelOptions.FirstOrDefault(o => o.Label == value.ColorLabel);
        SelectedPictures.Clear();
        SelectedPictures.Add(value);
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
        ActiveKeywords.Clear();
        var uniqueKeywords = SelectedPictures.SelectMany(p => p.Keywords).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!SelectedPictures.Any() && SelectedPicture != null) {
            uniqueKeywords = SelectedPicture.Keywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var kw in uniqueKeywords.OrderBy(k => k)) {
            ActiveKeywords.Add(kw);
        }
    }

    private void UpdateQuickTagStates() {
        var activeTags = SelectedPictures.SelectMany(p => p.Keywords).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!SelectedPictures.Any() && SelectedPicture != null) {
            activeTags = SelectedPicture.Keywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        IsQuickTag1Active = activeTags.Contains("Selected");
        IsQuickTag2Active = activeTags.Contains("Review");
        IsQuickTag3Active = activeTags.Contains("Highlight");
        IsQuickTag4Active = activeTags.Contains("Portrait");
        IsQuickTag5Active = activeTags.Contains("Landscape");
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
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try {
            PreviewImage = await ImageHelper.LoadAndOrientAsync(previewPath, 600);
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
            pictureVm.CurationStatus = status; // Trigger VM property update (which also updates model & notifies UI)
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
        // Trigger the metadata editing mode
        Log.Information("Edit metadata for {Name}", SelectedPicture?.Name);
    }

    [RelayCommand]
    private void DeleteAsset() {
        // Trigger the asset removal workflow
        Log.Information("Delete asset {Name}", SelectedPicture?.Name);
    }

    [RelayCommand]
    private void AddKeyword(string keyword) {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        var trimmed = keyword.Trim();

        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        bool changed = false;
        foreach (var picVm in targetVms) {
            if (!picVm.Keywords.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) {
                picVm.AddKeyword(trimmed);
                _curationQueue.Enqueue(picVm.Picture);
                changed = true;
            }
        }

        if (changed) {
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            WeakReferenceMessenger.Default.Send(new PictureKeywordsChangedMessage(targetVms));
        }
    }

    [RelayCommand]
    private void RemoveKeyword(string keyword) {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        var trimmed = keyword.Trim();

        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        bool changed = false;
        foreach (var picVm in targetVms) {
            var existing = picVm.Keywords.FirstOrDefault(k => k.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (existing != null) {
                picVm.RemoveKeyword(existing);
                _curationQueue.Enqueue(picVm.Picture);
                changed = true;
            }
        }

        if (changed) {
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
    private void ToggleQuickTag(string tag) {
        if (string.IsNullOrWhiteSpace(tag)) return;

        var targetVms = SelectedPictures.Any() ? SelectedPictures.ToList() : new List<PictureItemViewModel>();
        if (!targetVms.Any() && SelectedPicture != null) {
            targetVms.Add(SelectedPicture);
        }

        bool changed = false;
        foreach (var picVm in targetVms) {
            var exists = picVm.Keywords.Contains(tag, StringComparer.OrdinalIgnoreCase);
            if (exists) {
                picVm.RemoveKeyword(tag);
            } else {
                picVm.AddKeyword(tag);
            }
            _curationQueue.Enqueue(picVm.Picture);
            changed = true;
        }

        if (changed) {
            UpdateActiveKeywords();
            UpdateQuickTagStates();
            WeakReferenceMessenger.Default.Send(new PictureKeywordsChangedMessage(targetVms));
        }
    }
}
