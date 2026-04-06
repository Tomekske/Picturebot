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
}
