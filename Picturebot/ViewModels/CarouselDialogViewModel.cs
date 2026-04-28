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
using Picturebot.Utilities;
using Picturebot.Views;
using Serilog;

namespace Picturebot.ViewModels;

public partial class CarouselDialogViewModel : ViewModelBase {
    private readonly Action? _closeAction;
    private readonly INodeService _nodeService;
    private readonly ICurationQueue _curationQueue;
    private readonly List<PictureItemViewModel> _pictures;

    [ObservableProperty]
    private string _counterText = string.Empty;

    [ObservableProperty]
    private int _currentIndex;

    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private PictureItemViewModel _currentPicture;

    [ObservableProperty]
    private ObservableCollection<Bitmap?> _previews = new();

    [ObservableProperty]
    private int _sharpness;

    public CarouselDialogViewModel(IEnumerable<PictureItemViewModel> pictures, PictureItemViewModel? selectedPicture,
        INodeService nodeService, ICurationQueue curationQueue, Action? closeAction = null) {
        _nodeService = nodeService;
        _curationQueue = curationQueue;
        _closeAction = closeAction;
        _pictures = pictures.ToList();

        var initial = selectedPicture ?? _pictures.FirstOrDefault();
        if (initial == null) {
            throw new ArgumentException("Pictures list cannot be empty");
        }

        // Initialize Previews collection with nulls
        foreach (var _ in _pictures) {
            Previews.Add(null);
        }

        _currentPicture = initial;
        _currentIndex = _pictures.IndexOf(initial);
        UpdateState();
    }

    private void UpdateState() {
        CounterText = $"{CurrentIndex + 1} / {_pictures.Count}";
        Sharpness = CurrentPicture.Picture.Sharpness;
        _ = LoadNearbyPreviewsAsync();
    }

    private async Task LoadNearbyPreviewsAsync() {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try {
            // Load current, prev, and next
            var indicesToLoad = new[] { CurrentIndex, CurrentIndex - 1, CurrentIndex + 1 }
                .Where(i => i >= 0 && i < _pictures.Count)
                .ToList();

            foreach (var i in indicesToLoad) {
                if (Previews[i] == null) {
                    var path = _pictures[i].Picture.SubFolder?.Preview;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                        // Increase to 2400 for high-res sharpness
                        Previews[i] = await ImageHelper.LoadAndOrientAsync(path, 2400);
                    }
                }
            }

            // Cleanup distant previews to save memory
            for (int i = 0; i < Previews.Count; i++) {
                if (Previews[i] != null && Math.Abs(i - CurrentIndex) > 5) {
                    Previews[i]?.Dispose();
                    Previews[i] = null;
                }
            }
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load carousel previews");
        }
    }

    partial void OnCurrentIndexChanged(int value) {
        if (value >= 0 && value < _pictures.Count) {
            CurrentPicture = _pictures[value];
            UpdateState();
        }
    }

    [RelayCommand]
    private void Next() {
        if (CurrentIndex < _pictures.Count - 1) {
            CurrentIndex++;
        } else {
            CurrentIndex = 0;
        }
    }

    [RelayCommand]
    private void Previous() {
        if (CurrentIndex > 0) {
            CurrentIndex--;
        } else {
            CurrentIndex = _pictures.Count - 1;
        }
    }

    [RelayCommand]
    private async Task SetCurationStatus(CurationStatus status) {
        try {
            CurrentPicture.Picture.CurationStatus = status;
            CurrentPicture.CurationStatus = status; // Triggers UI update in Grid/Details
            
            _curationQueue.Enqueue(CurrentPicture.Picture);
            await Task.CompletedTask;
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
