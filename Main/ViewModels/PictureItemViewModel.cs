using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Database.Domain.Entities;
using Serilog;

namespace Main.ViewModels;

public partial class PictureItemViewModel : ViewModelBase, IDisposable {
    private readonly Picture _picture;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Domain.Enums.CurationStatus _curationStatus;

    public Picture Picture => _picture;
    public string Name => _picture.Name;

    public PictureItemViewModel(Picture picture) {
        _picture = picture;
        _curationStatus = picture.CurationStatus;
    }

    public async Task LoadThumbnailAsync(int width) {
        if (string.IsNullOrEmpty(_picture.SubFolder?.Thumbnail) || !File.Exists(_picture.SubFolder.Thumbnail)) {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try {
            var path = _picture.SubFolder.Thumbnail;
            var bitmap = await Task.Run(() => {
                using var stream = File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, width);
            }, _cts.Token);

            Thumbnail = bitmap;
        } catch (OperationCanceledException) {
            // Loading was cancelled
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load thumbnail for {Name} at {Path}", Name, _picture.SubFolder.Thumbnail);
        }
    }

    public void Dispose() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        Thumbnail?.Dispose();
        Thumbnail = null;
    }
}
