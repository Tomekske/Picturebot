using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Picturebot.Views;

public partial class AddAlbumDialog : UserControl {
    public AddAlbumDialog() {
        InitializeComponent();
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}
