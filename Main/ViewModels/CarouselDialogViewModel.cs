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
using Domain.Enums;
using Graph.Domain.Interfaces;
using Main.Views;
using Serilog;

namespace Main.ViewModels;

public partial class CarouselDialogViewModel : ViewModelBase {
    private readonly INodeService _nodeService;
    private readonly List<PictureItemViewModel> _pictures;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private PictureItemViewModel _currentPicture;

    [ObservableProperty]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private string _counterText = string.Empty;

    [ObservableProperty]
    private int _sharpness;

    private readonly Action? _closeAction;

    public CarouselDialogViewModel(IEnumerable<PictureItemViewModel> pictures, PictureItemViewModel? selectedPicture, INodeService nodeService, Action? closeAction = null) {
        _nodeService = nodeService;
        _closeAction = closeAction;
        _pictures = pictures.ToList();
        
        var initial = selectedPicture ?? _pictures.FirstOrDefault();
        if (initial == null) throw new ArgumentException("Pictures list cannot be empty");
        
        _currentPicture = initial;
        UpdateState();
    }

    private void UpdateState() {
        var index = _pictures.IndexOf(CurrentPicture);
        CounterText = $"{index + 1} / {_pictures.Count}";
        Sharpness = CurrentPicture.Picture.Sharpness;
        _ = LoadPreviewAsync(CurrentPicture);
    }

    private async Task LoadPreviewAsync(PictureItemViewModel picVm) {
        var previewPath = picVm.Picture.SubFolder?.Preview;
        if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath)) {
            Log.Warning("No preview available for {Name}", picVm.Name);
            PreviewImage = null;
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try {
            PreviewImage = await Utilities.ImageHelper.LoadAndOrientAsync(previewPath, 1200);
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load carousel preview for {Name}", picVm.Name);
        }
    }

    [RelayCommand]
    private void Next() {
        var index = _pictures.IndexOf(CurrentPicture);
        var nextIndex = (index + 1) % _pictures.Count;
        CurrentPicture = _pictures[nextIndex];
        UpdateState();
    }

    [RelayCommand]
    private void Previous() {
        var index = _pictures.IndexOf(CurrentPicture);
        var prevIndex = (index - 1 + _pictures.Count) % _pictures.Count;
        CurrentPicture = _pictures[prevIndex];
        UpdateState();
    }

    [RelayCommand]
    private async Task SetCurationStatus(CurationStatus status) {
        try {
            CurrentPicture.Picture.CurationStatus = status;
            CurrentPicture.CurationStatus = status; // Triggers UI update in Grid/Details
            await _nodeService.UpdateNodeAsync(CurrentPicture.Picture);
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update curation in carousel for {Name}", CurrentPicture.Name);
        }
    }

    [RelayCommand]
    private void Close() {
        if (_closeAction != null) {
            _closeAction.Invoke();
        } else {
            MainWindow.DialogManager.DismissDialog();
        }
    }
}
