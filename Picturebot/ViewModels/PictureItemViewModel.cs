using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Database.Domain.Entities;
using Domain.Enums;
using Picturebot.Utilities;
using Picturebot.Services;
using Serilog;

namespace Picturebot.ViewModels;

public partial class PictureItemViewModel : ViewModelBase, IDisposable {
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private CurationStatus _curationStatus;

    [ObservableProperty]
    private ColorLabel _colorLabel;

    [ObservableProperty]
    private int _rating;

    [ObservableProperty]
    private ProcessingState _processingState;

    [ObservableProperty]
    private bool _isBest;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private string? _groupName;

    [ObservableProperty]
    private int _burstIndex;

    [ObservableProperty]
    private int _burstPosition;

    [ObservableProperty]
    private int _burstTotal;

    public PictureItemViewModel(Picture picture) {
        Picture = picture;
        _curationStatus = picture.CurationStatus;
        _colorLabel = picture.ColorLabel;
        _rating = picture.Rating;
        _processingState = picture.ProcessingState;
    }

    public Picture Picture { get; }

    public bool IsVisible { get; set; }

    public string Name => Picture.Name;

    public void CancelLoading() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() {
        CancelLoading();
        Thumbnail = null;
    }

    public async Task LoadThumbnailAsync(int targetHeight) {
        if (Thumbnail != null) {
            return;
        }

        var path = Picture.SubFolder?.Thumbnail;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
            path = Picture.SubFolder?.Raw;
        }
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
            path = Picture.SubFolder?.Preview;
        }
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try {
            var priority = IsVisible ? 0 : 1;
            var bitmap = await ThumbnailRegistry.Instance.QueueRequestAsync(path, targetHeight, priority, token);
            
            if (!token.IsCancellationRequested && bitmap != null) {
                Thumbnail = bitmap;
            }
        } catch (OperationCanceledException) {
            // Loading was cancelled
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load thumbnail for {Name} via registry", Name);
        }
    }
}
