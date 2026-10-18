using Avalonia.Controls;
using Avalonia.Input;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class NewNodeDialog : UserControl {
    public NewNodeDialog() {
        InitializeComponent();
    }

    private void Row_Tapped(object? sender, TappedEventArgs e) {
        if (sender is Control { DataContext: LocationRow row } && row.Command.CanExecute(null)) {
            row.Command.Execute(null);
        }
    }

    // Enter opens the first row (or creates the typed folder); Backspace in an empty box goes up one level.
    private void QueryBox_KeyDown(object? sender, KeyEventArgs e) {
        if (DataContext is not NewNodeDialogViewModel vm) return;

        if (e.Key == Key.Enter) {
            vm.ActivateFirstRow();
            e.Handled = true;
        } else if (e.Key == Key.Back && string.IsNullOrEmpty(vm.Query)) {
            vm.GoUp();
            e.Handled = true;
        }
    }
}
