using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Picturebot.Views;

public partial class ImportProgressDialog : UserControl {
    public ImportProgressDialog() {
        InitializeComponent();
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}
