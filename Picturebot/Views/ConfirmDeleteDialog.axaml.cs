using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Picturebot.Views;

public partial class ConfirmDeleteDialog : UserControl {
    public ConfirmDeleteDialog() {
        InitializeComponent();
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }
}
