using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Picturebot.Views;

public partial class EditNodeDialog : UserControl {
    public EditNodeDialog() {
        InitializeComponent();
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}
