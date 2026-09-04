using Avalonia.Controls;
using Avalonia.Input;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class NavigationPaneView : UserControl {
    public NavigationPaneView() {
        InitializeComponent();
    }

    private void SearchInput_KeyDown(object? sender, KeyEventArgs e) {
        if (DataContext is not NavigationPaneViewModel vm) return;

        if (e.Key == Key.Enter) {
            vm.ExecuteSearchCommand.Execute(null);
            e.Handled = true;
        } else if (e.Key == Key.Escape) {
            vm.ClearSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void SearchInput_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (DataContext is not NavigationPaneViewModel vm) return;
        if (sender is AutoCompleteBox box && box.SelectedItem is string selectedText && !string.IsNullOrWhiteSpace(selectedText)) {
            vm.ExecuteSearchCommand.Execute(selectedText);
        }
    }
}
