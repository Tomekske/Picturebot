using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Main.Views;

public partial class AddAlbumDialog : UserControl {
    public AddAlbumDialog() {
        InitializeComponent();
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}
