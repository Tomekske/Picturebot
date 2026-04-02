using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Main.Views;

public partial class ImportProgressDialog : UserControl {
    public ImportProgressDialog() {
        InitializeComponent();
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}
