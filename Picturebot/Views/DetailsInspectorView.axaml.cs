using Avalonia.Controls;
using Avalonia.Input;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class DetailsInspectorView : UserControl {
    public DetailsInspectorView() {
        InitializeComponent();
    }

    private void TagInput_KeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) {
            if (DataContext is DetailsInspectorViewModel vm) {
                vm.CommitNewKeywordCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
