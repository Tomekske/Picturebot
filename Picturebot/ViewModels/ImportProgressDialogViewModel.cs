using CommunityToolkit.Mvvm.ComponentModel;
using Graph.Domain.DTOs;

namespace Picturebot.ViewModels;

public partial class ImportProgressDialogViewModel : ViewModelBase {
    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private double _percentage;

    [ObservableProperty]
    private int _processed;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private int _total;

    public void Update(ImportProgress progress) {
        Processed = progress.Processed;
        Total = progress.Total;
        CurrentFile = progress.CurrentFile;
        Percentage = progress.Percentage;
        StatusText = $"Processing image {Processed} of {Total}";
    }

    public void Update(ImportBatchProgress progress) {
        if (progress.CurrentAlbumProgress != null) {
            Processed = progress.CurrentAlbumProgress.Processed;
            Total = progress.CurrentAlbumProgress.Total;
            CurrentFile = progress.CurrentAlbumProgress.CurrentFile;
            Percentage = progress.CurrentAlbumProgress.Percentage;
            StatusText = $"[{progress.ProcessedAlbums}/{progress.TotalAlbums}] {progress.CurrentAlbumName}: Image {Processed} of {Total}";
        } else {
            Processed = 0;
            Total = 0;
            CurrentFile = string.Empty;
            Percentage = 0;
            StatusText = $"[{progress.ProcessedAlbums}/{progress.TotalAlbums}] {progress.CurrentAlbumName}";
        }
    }
}
