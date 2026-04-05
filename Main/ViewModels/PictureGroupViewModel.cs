using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Main.ViewModels;

public partial class PictureGroupViewModel : ViewModelBase {
    [ObservableProperty]
    private string _date = string.Empty;

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private bool _isBurstGroup;

    [ObservableProperty]
    private ObservableCollection<PictureItemViewModel> _pictures = new();

    public PictureGroupViewModel(string date, string header, ObservableCollection<PictureItemViewModel> pictures, bool isBurstGroup = false) {
        Date = date;
        Header = header;
        Pictures = pictures;
        IsBurstGroup = isBurstGroup;
    }
}
