using CommunityToolkit.Mvvm.ComponentModel;
using Graph.Domain.DTOs;

namespace Main.ViewModels;

public partial class ImportProgressDialogViewModel : ViewModelBase {
    [ObservableProperty]
    private int _processed;

    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private double _percentage;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public void Update(ImportProgress progress) {
        Processed = progress.Processed;
        Total = progress.Total;
        CurrentFile = progress.CurrentFile;
        Percentage = progress.Percentage;
        StatusText = $"Processing image {Processed} of {Total}";
    }
}
