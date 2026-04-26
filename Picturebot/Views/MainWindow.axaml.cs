using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.Services;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Picturebot.Views;

public partial class MainWindow : SukiWindow {
    public static ISukiDialogManager DialogManager = new SukiDialogManager();
    public static ISukiToastManager ToastManager = new SukiToastManager();

    public MainWindow() {
        Instance = this;
        InitializeComponent();
        DialogHost.Manager = DialogManager;
        ToastHost.Manager = ToastManager;
    }

    public static MainWindow? Instance { get; private set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);

        var properties = e.GetCurrentPoint(this).Properties;
        var navigationService = App.Services?.GetService<INavigationService>();

        if (navigationService == null) return;

        if (properties.IsXButton1Pressed) {
            navigationService.GoBack();
            e.Handled = true;
        } else if (properties.IsXButton2Pressed) {
            navigationService.GoForward();
            e.Handled = true;
        }
    }
}
