using Avalonia.Controls;
using Avalonia.Input;
using Main.ViewModels;

namespace Main.Views;

public partial class CarouselDialogView : UserControl {
    public CarouselDialogView() {
        InitializeComponent();
        
        // Ensure the control can receive focus for keyboard events
        Focusable = true;
        
        // Focus once loaded
        Loaded += (s, e) => Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);

        if (DataContext is not CarouselDialogViewModel vm) return;

        switch (e.Key) {
            case Key.Escape:
                vm.CloseCommand.Execute(null);
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
