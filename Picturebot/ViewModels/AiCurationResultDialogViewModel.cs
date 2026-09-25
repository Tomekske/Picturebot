using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Enums;
using Graph.Domain.Interfaces;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Picturebot.Services;
using Picturebot.Views;
using Serilog;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class AiCurationItemResultViewModel : ObservableObject {
    public int Index { get; set; }
    public PictureItemViewModel PictureVm { get; set; } = null!;
    public string Name => PictureVm.Name;
    public string Feedback { get; set; } = string.Empty;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isBest;

    public string BadgeText => IsBest ? "★ BEST PICK" : "ALTERNATIVE";
    public string BadgeBackground => IsBest ? "#1E4620" : "#4E260A";
    public string BadgeForeground => IsBest ? "#4ADE80" : "#FB923C";
    public string CardBorderBrush => IsBest ? "#2ECC71" : "#2D2D36";
    public string CardBackground => IsBest ? "#16231A" : "#1B1B22";
    public bool CanSetAsBest => !IsBest;

    public Action<AiCurationItemResultViewModel>? SetAsBestAction { get; set; }

    partial void OnIsBestChanged(bool value) {
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(BadgeBackground));
        OnPropertyChanged(nameof(BadgeForeground));
        OnPropertyChanged(nameof(CardBorderBrush));
        OnPropertyChanged(nameof(CardBackground));
        OnPropertyChanged(nameof(CanSetAsBest));
    }

    [RelayCommand]
    private void SelectAsBest() {
        SetAsBestAction?.Invoke(this);
    }

    public async Task LoadDialogThumbnailAsync() {
        if (Thumbnail != null) return;
        if (PictureVm.Thumbnail != null) {
            Thumbnail = PictureVm.Thumbnail;
            return;
        }

        var picture = PictureVm.Picture;
        string? path = null;
        if (!string.IsNullOrEmpty(picture.SubFolder?.Thumbnail) && File.Exists(picture.SubFolder.Thumbnail)) {
            path = picture.SubFolder.Thumbnail;
        } else if (!string.IsNullOrEmpty(picture.SubFolder?.Preview) && File.Exists(picture.SubFolder.Preview)) {
            path = picture.SubFolder.Preview;
        } else if (!string.IsNullOrEmpty(picture.SubFolder?.Raw) && File.Exists(picture.SubFolder.Raw)) {
            path = picture.SubFolder.Raw;
        } else if (!string.IsNullOrEmpty(picture.SubFolder?.Picked) && File.Exists(picture.SubFolder.Picked)) {
            path = picture.SubFolder.Picked;
        }

        if (string.IsNullOrEmpty(path)) return;

        try {
            var bmp = await ThumbnailRegistry.Instance.QueueRequestAsync(path, 120, 0, CancellationToken.None);
            if (bmp != null) {
                Dispatcher.UIThread.Post(() => Thumbnail = bmp);
                return;
            }

            // Fallback: direct decode or EXIF thumbnail
            await Task.Run(() => {
                try {
                    using var fs = File.OpenRead(path);
                    var decoded = Bitmap.DecodeToHeight(fs, 120);
                    Dispatcher.UIThread.Post(() => Thumbnail = decoded);
                } catch {
                    try {
                        var directories = ImageMetadataReader.ReadMetadata(path);
                        var thumbDir = directories.OfType<ExifThumbnailDirectory>().FirstOrDefault();
                        if (thumbDir != null &&
                            thumbDir.TryGetInt32(ExifThumbnailDirectory.TagThumbnailOffset, out var offset) &&
                            thumbDir.TryGetInt32(ExifThumbnailDirectory.TagThumbnailLength, out var length)) {
                            using var rawFs = File.OpenRead(path);
                            var buffer = new byte[length];
                            rawFs.Position = offset;
                            var read = rawFs.Read(buffer, 0, length);
                            if (read == length) {
                                using var ms = new MemoryStream(buffer);
                                var rawBmp = Bitmap.DecodeToHeight(ms, 120);
                                Dispatcher.UIThread.Post(() => Thumbnail = rawBmp);
                            }
                        }
                    } catch {
                        // Ignored
                    }
                }
            });
        } catch (Exception ex) {
            Log.Warning(ex, "Failed to load dialog thumbnail for {Name}", Name);
        }
    }
}

public class AiCurationGroupResultViewModel {
    public string Header { get; set; } = string.Empty;
    public ObservableCollection<AiCurationItemResultViewModel> Items { get; set; } = new();

    public void SetBestPick(AiCurationItemResultViewModel winner) {
        foreach (var item in Items) {
            item.IsBest = (item == winner);
        }
    }
}

public partial class AiCurationResultDialogViewModel : ViewModelBase {
    private readonly ICurationQueue _curationQueue;

    public ObservableCollection<AiCurationGroupResultViewModel> Groups { get; }

    public int TotalBursts => Groups.Count;
    public int TotalPictures => Groups.Sum(g => g.Items.Count);

    public AiCurationResultDialogViewModel(
        IEnumerable<AiCurationGroupResultViewModel> groups,
        ICurationQueue curationQueue) {
        Groups = new ObservableCollection<AiCurationGroupResultViewModel>(groups);
        _curationQueue = curationQueue;

        foreach (var group in Groups) {
            foreach (var item in group.Items) {
                item.SetAsBestAction = group.SetBestPick;
                _ = item.LoadDialogThumbnailAsync();
            }
        }
    }

    [RelayCommand]
    private void Apply() {
        try {
            int bestCount = 0;
            int altCount = 0;

            foreach (var group in Groups) {
                foreach (var item in group.Items) {
                    var picVm = item.PictureVm;
                    var pic = picVm.Picture;

                    if (item.IsBest) {
                        picVm.ColorLabel = ColorLabel.Green;
                        pic.ColorLabel = ColorLabel.Green;
                        picVm.Rating = 5;
                        pic.Rating = 5;
                        picVm.IsBest = true;
                        picVm.IsAiBest = true;
                        bestCount++;
                    } else {
                        picVm.ColorLabel = ColorLabel.Orange;
                        pic.ColorLabel = ColorLabel.Orange;
                        picVm.Rating = 0;
                        pic.Rating = 0;
                        picVm.IsBest = false;
                        picVm.IsAiBest = false;
                        altCount++;
                    }

                    if (!string.IsNullOrEmpty(item.Feedback)) {
                        picVm.AiFeedback = item.Feedback;
                        if (pic.Metrics != null) {
                            pic.Metrics.AiFeedback = item.Feedback;
                        }
                    }

                    _curationQueue.Enqueue(pic);
                }
            }

            Log.Information("Applied AI curation suggestions for {Bursts} burst(s): {Best} picked, {Alt} alternatives",
                TotalBursts, bestCount, altCount);

            MainWindow.DialogManager.DismissDialog();

            MainWindow.ToastManager.CreateToast()
                .OfType(Avalonia.Controls.Notifications.NotificationType.Success)
                .WithTitle("AI Curation Applied")
                .WithContent($"Updated {bestCount} best pick(s) to Green (5★) and {altCount} to Orange (0★).")
                .Dismiss().After(TimeSpan.FromSeconds(4))
                .Queue();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to apply AI curation results");
            MainWindow.DialogManager.DismissDialog();
        }
    }

    [RelayCommand]
    private void Cancel() {
        MainWindow.DialogManager.DismissDialog();
    }
}
