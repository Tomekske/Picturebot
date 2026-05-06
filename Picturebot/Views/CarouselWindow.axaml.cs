using Avalonia.Controls;
using Avalonia.Input;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class CarouselWindow : Window {
    public CarouselWindow() {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);

        if (DataContext is not CarouselDialogViewModel vm) {
            return;
        }

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
            case Key.P:
                vm.SetCurationStatusCommand.Execute(Domain.Enums.CurationStatus.Flagged);
                e.Handled = true;
                break;
            case Key.X:
                vm.SetCurationStatusCommand.Execute(Domain.Enums.CurationStatus.Rejected);
                e.Handled = true;
                break;
            case Key.U:
                vm.SetCurationStatusCommand.Execute(Domain.Enums.CurationStatus.Unflagged);
                e.Handled = true;
                break;
        }
    }
}
