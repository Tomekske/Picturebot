using System;
using Avalonia.Controls;
using Avalonia.Input;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.Services;
using Picturebot.ViewModels;
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

    private static bool MatchesGesture(KeyEventArgs e, string? shortcutText) {
        if (string.IsNullOrWhiteSpace(shortcutText) || shortcutText.Equals("None", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        try {
            var gesture = KeyGesture.Parse(shortcutText);
            if (gesture.Matches(e)) {
                return true;
            }

            if (gesture.KeyModifiers == e.KeyModifiers) {
                if (gesture.Key >= Key.D0 && gesture.Key <= Key.D9) {
                    var offset = gesture.Key - Key.D0;
                    var equivalentNumPadKey = Key.NumPad0 + offset;
                    if (e.Key == equivalentNumPadKey) {
                        return true;
                    }
                }
            }
        } catch {
            // Ignored
        }

        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        var focusedElement = FocusManager?.GetFocusedElement();
        if (focusedElement is TextBox || focusedElement is NumericUpDown || focusedElement is ComboBox) {
            return;
        }

        var settingsService = App.Services?.GetService<ISettingsService>();
        var settings = settingsService?.Current;
        if (settings != null && DataContext is MainWindowViewModel vm && vm.GalleryVM != null) {
            if (MatchesGesture(e, settings.RedLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.Red);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.OrangeLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.Orange);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.YellowLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.Yellow);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.GreenLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.Green);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.BlueLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.Blue);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.PinkLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.Pink);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.PurpleLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.Purple);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.NoneLabelShortcut)) {
                vm.GalleryVM.SetColorLabelCommand.Execute(ColorLabel.None);
                e.Handled = true;
                return;
            }
        }
        
        base.OnKeyDown(e);
    }

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
