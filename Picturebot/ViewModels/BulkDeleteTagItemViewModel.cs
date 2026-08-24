using CommunityToolkit.Mvvm.ComponentModel;

namespace Picturebot.ViewModels;

public partial class BulkDeleteTagItemViewModel : ObservableObject {
    [ObservableProperty]
    private string _tagName = string.Empty;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;
}
