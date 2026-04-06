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
    private bool _isBest;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    public PictureItemViewModel(Picture picture) {
        Picture = picture;
        _curationStatus = picture.CurationStatus;
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
