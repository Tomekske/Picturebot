using CommunityToolkit.Mvvm.ComponentModel;

namespace Picturebot.ViewModels;

public partial class BulkAddTagItemViewModel : ObservableObject {
    [ObservableProperty]
    private string _tagName = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
