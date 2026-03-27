using Avalonia.Controls;

namespace Main.Views;

public partial class StatusBarView : UserControl {
    public StatusBarView() {
        InitializeComponent();

        // If you aren't using a ViewLocator or setting this from MainWindow:
        if (!Design.IsDesignMode) {
            DataContext = new ViewModels.StatusBarViewModel();
        }
    }
}
