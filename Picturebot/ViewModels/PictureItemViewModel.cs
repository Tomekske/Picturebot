using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Database.Domain.Entities;
using Domain.Enums;
using Picturebot.Utilities;
using Serilog;

namespace Picturebot.ViewModels;

public partial class PictureItemViewModel : ViewModelBase, IDisposable {
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private CurationStatus _curationStatus;

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
        _processingState = picture.ProcessingState;
    }

    public Picture Picture { get; }

    public string Name => Picture.Name;

    public void Dispose() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        Thumbnail?.Dispose();
        Thumbnail = null;
    }

    public async Task LoadThumbnailAsync(int targetHeight) {
        if (string.IsNullOrEmpty(Picture.SubFolder?.Thumbnail) || !File.Exists(Picture.SubFolder.Thumbnail)) {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try {
            var path = Picture.SubFolder.Thumbnail;
            Thumbnail = await ImageHelper.LoadAndOrientAsync(path, targetHeight);
        } catch (OperationCanceledException) {
            // Loading was cancelled
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load thumbnail for {Name} at {Path}", Name, Picture.SubFolder.Thumbnail);
        }
    }
}
