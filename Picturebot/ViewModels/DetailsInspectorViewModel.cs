using System;
using System.Collections.Generic;
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

public partial class DetailsInspectorViewModel : ViewModelBase, IRecipient<PictureSelectedMessage> {
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
            return;
        }

        value.PropertyChanged += OnPicturePropertyChanged;

        SelectedColorLabelOption = ColorLabelOptions.FirstOrDefault(o => o.Label == value.ColorLabel);
        await LoadPreviewAsync(value);
    }

    private void OnPicturePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(PictureItemViewModel.ColorLabel) && SelectedPicture != null) {
            var newOption = ColorLabelOptions.FirstOrDefault(o => o.Label == SelectedPicture.ColorLabel);
            if (SelectedColorLabelOption != newOption) {
                SelectedColorLabelOption = newOption;
            }
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
        if (SelectedPicture == null) {
            return;
        }

        IsBusy = true;
        try {
            SelectedPicture.Picture.CurationStatus = status;
            SelectedPicture.CurationStatus = status; // Update the VM property to trigger UI update

            _curationQueue.Enqueue(SelectedPicture.Picture);
            await Task.CompletedTask;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update curation status for {Name}", SelectedPicture.Name);
        } finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetColorLabel(ColorLabel label) {
        if (SelectedPicture == null) {
            return;
        }

        try {
            SelectedPicture.Picture.ColorLabel = label;
            SelectedPicture.ColorLabel = label;
            _curationQueue.Enqueue(SelectedPicture.Picture);
            await Task.CompletedTask;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update color label for {Name}", SelectedPicture.Name);
        }
    }

    [RelayCommand]
    private async Task SetRating(string ratingStr) {
        if (SelectedPicture == null || !int.TryParse(ratingStr, out var rating)) {
            return;
        }

        try {
            SelectedPicture.Picture.Rating = rating;
            SelectedPicture.Rating = rating;
            _curationQueue.Enqueue(SelectedPicture.Picture);
            await Task.CompletedTask;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update rating for {Name}", SelectedPicture.Name);
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
}
