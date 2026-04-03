using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Main.ViewModels;

namespace Main.Views;

public partial class CarouselWindow : Window {
    public CarouselWindow() {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);

        if (DataContext is not CarouselDialogViewModel vm) return;

        switch (e.Key) {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Left:
                vm.PreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right:
                vm.NextCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
