using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Database.Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private Orientation _orientation;

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

    [ObservableProperty]
    private ObservableCollection<string> _keywords = new();

    [ObservableProperty]
    private string? _aiFeedback;

    [ObservableProperty]
    private bool _isAiBest;

    public PictureItemViewModel(Picture picture) {
        Picture = picture;
        _curationStatus = picture.CurationStatus;
        _colorLabel = picture.ColorLabel;
        _rating = picture.Rating;
        _orientation = picture.Orientation;
        _processingState = picture.ProcessingState;
        _aiFeedback = picture.Metrics?.AiFeedback;
        _keywords = new ObservableCollection<string>(picture.Keywords ?? new List<string>());
    }

    public Picture Picture { get; }

    public int RawExifOrientation => Picture.RawExifOrientation;

    public bool IsVisible { get; set; }

    public string Name => Picture.Name;

    /// <summary>File types present for this picture, e.g. "ARW + JPG".</summary>
    public string FileTypesText {
        get {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Picture.Extension)) {
                parts.Add(Picture.Extension.TrimStart('.').ToUpperInvariant());
            }
            var preview = Picture.SubFolder?.Preview;
            if (!string.IsNullOrEmpty(preview) && File.Exists(preview)) {
                parts.Add("JPG");
            }
            return parts.Count > 0 ? string.Join(" + ", parts) : "Unknown format";
        }
    }

    public void AddKeyword(string keyword) {
        if (!string.IsNullOrWhiteSpace(keyword)) {
            var trimmed = keyword.Trim();
            if (!Keywords.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) {
                Keywords.Add(trimmed);
                Picture.Keywords = Keywords.ToList();
                Picture.KeywordsJson = System.Text.Json.JsonSerializer.Serialize(Picture.Keywords);
                OnPropertyChanged(nameof(Keywords));
            }
        }
    }

    public void RemoveKeyword(string keyword) {
        if (!string.IsNullOrWhiteSpace(keyword)) {
            var trimmed = keyword.Trim();
            var existing = Keywords.FirstOrDefault(k => k.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (existing != null) {
                Keywords.Remove(existing);
                Picture.Keywords = Keywords.ToList();
                Picture.KeywordsJson = System.Text.Json.JsonSerializer.Serialize(Picture.Keywords);
                OnPropertyChanged(nameof(Keywords));
            }
        }
    }

    public void NotifyKeywordsChanged() {
        OnPropertyChanged(nameof(Keywords));
    }

    public void CancelLoading() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() {
        CancelLoading();
        Thumbnail = null;
    }

    private string? _resolvedThumbnailPath;

    public void ResolveThumbnailPath() {
        if (_resolvedThumbnailPath != null) return;

        var path = Picture.SubFolder?.Thumbnail;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
            _resolvedThumbnailPath = path;
            return;
        }
        path = Picture.SubFolder?.Raw;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
            _resolvedThumbnailPath = path;
            return;
        }
        path = Picture.SubFolder?.Preview;
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
            _resolvedThumbnailPath = path;
            return;
        }
    }

    public async Task LoadThumbnailAsync(int targetHeight) {
        if (Thumbnail != null) {
            return;
        }

        if (string.IsNullOrEmpty(_resolvedThumbnailPath)) {
            // Resolve on background thread pool if not already pre-resolved
            await Task.Run(() => ResolveThumbnailPath()).ConfigureAwait(false);
            if (string.IsNullOrEmpty(_resolvedThumbnailPath)) {
                return;
            }
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try {
            var priority = IsVisible ? 0 : 1;
            var bitmap = await ThumbnailRegistry.Instance.QueueRequestAsync(_resolvedThumbnailPath, targetHeight, priority, Picture.RawExifOrientation, token);
            
            if (!token.IsCancellationRequested && bitmap != null) {
                Thumbnail = bitmap;
            }
        } catch (OperationCanceledException) {
            // Loading was cancelled
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load thumbnail for {Name} via registry", Name);
        }
    }

    /// <summary>
    ///     Swaps a fallback thumbnail (the RAW's embedded preview, often letterboxed with black bars) for the
    ///     generated one once processing has written it. The old bitmap stays visible until the new one is ready.
    /// </summary>
    public async Task UpgradeToGeneratedThumbnailAsync(int targetHeight) {
        var generated = Picture.SubFolder?.Thumbnail;
        if (string.IsNullOrEmpty(generated) || _resolvedThumbnailPath == generated || !File.Exists(generated)) {
            return;
        }

        _resolvedThumbnailPath = generated;
        if (Thumbnail == null) {
            // Not loaded yet (off-screen); the normal load will pick up the generated thumbnail.
            return;
        }

        try {
            var bitmap = await ThumbnailRegistry.Instance.QueueRequestAsync(generated, targetHeight, IsVisible ? 0 : 1, Picture.RawExifOrientation, CancellationToken.None);
            if (bitmap != null && _resolvedThumbnailPath == generated) {
                Thumbnail = bitmap;
            }
        } catch (Exception ex) {
            Log.Warning(ex, "Failed to load generated thumbnail for {Name}", Name);
        }
    }

    partial void OnRatingChanged(int value) {
        Picture.Rating = value;
    }

    partial void OnOrientationChanged(Orientation value) {
        Picture.Orientation = value;
    }

    partial void OnCurationStatusChanged(CurationStatus value) {
        Picture.CurationStatus = value;
    }

    partial void OnColorLabelChanged(ColorLabel value) {
        Picture.ColorLabel = value;
    }

    partial void OnProcessingStateChanged(ProcessingState value) {
        Picture.ProcessingState = value;
    }
}
