using Avalonia.Controls;
using Main.ViewModels;

namespace Main.Views;

public partial class SettingsDialog : UserControl {
    public SettingsDialog() {
        InitializeComponent();
        DataContext = new SettingsDialogViewModel();
    }
}
