using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class SettingsDialog : Window {
    public SettingsDialog() {
        InitializeComponent();
        var service = App.Services?.GetRequiredService<ISettingsService>();

        DataContext = service != null ? new SettingsDialogViewModel(service) : new SettingsDialogViewModel();
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e) {
        BeginMoveDrag(e);
    }

    public void OnInlineAutoCompleteGotFocus(object? sender, GotFocusEventArgs e) {
        if (sender is AutoCompleteBox acb) {
            acb.IsDropDownOpen = true;
        }
    }

    public void OnInlineAutoCompleteAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e) {
        if (sender is AutoCompleteBox acb) {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                acb.Focus();
                acb.IsDropDownOpen = true;
            }, Avalonia.Threading.DispatcherPriority.Input);
        }
    }

    public void OnShortcutKeyDown(object? sender, KeyEventArgs e) {
        if (sender is TextBox textBox) {
            if (e.Key == Key.Back || e.Key == Key.Delete) {
                UpdateViewModelShortcut(textBox, "None");
                e.Handled = true;
                TopLevel.GetTopLevel(textBox)?.FocusManager?.ClearFocus();
                return;
            }
            if (e.Key == Key.Escape) {
                e.Handled = true;
                TopLevel.GetTopLevel(textBox)?.FocusManager?.ClearFocus();
                return;
            }
            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) {
                return;
            }

            var modifiers = new List<string>();
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers.Add("Ctrl");
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers.Add("Shift");
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers.Add("Alt");
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers.Add("Windows");

            var keyName = e.Key.ToString();
            var shortcutText = modifiers.Count > 0 
                ? $"{string.Join("+", modifiers)}+{keyName}" 
                : keyName;

            UpdateViewModelShortcut(textBox, shortcutText);
            e.Handled = true;
            TopLevel.GetTopLevel(textBox)?.FocusManager?.ClearFocus();
        }
    }

    private void UpdateViewModelShortcut(TextBox textBox, string shortcutText) {
        if (DataContext is not SettingsDialogViewModel vm) return;

        var tag = textBox.Tag as string;
        switch (tag) {
            case "Red": vm.RedLabelShortcut = shortcutText; break;
            case "Orange": vm.OrangeLabelShortcut = shortcutText; break;
            case "Yellow": vm.YellowLabelShortcut = shortcutText; break;
            case "Green": vm.GreenLabelShortcut = shortcutText; break;
            case "Blue": vm.BlueLabelShortcut = shortcutText; break;
            case "Pink": vm.PinkLabelShortcut = shortcutText; break;
            case "Purple": vm.PurpleLabelShortcut = shortcutText; break;
            case "None": vm.NoneLabelShortcut = shortcutText; break;
            case "Fullscreen": vm.FullscreenShortcut = shortcutText; break;
            case "OpenInExplorer": vm.OpenInExplorerShortcut = shortcutText; break;
            case "Rating0": vm.Rating0Shortcut = shortcutText; break;
            case "Rating1": vm.Rating1Shortcut = shortcutText; break;
            case "Rating2": vm.Rating2Shortcut = shortcutText; break;
            case "Rating3": vm.Rating3Shortcut = shortcutText; break;
            case "Rating4": vm.Rating4Shortcut = shortcutText; break;
            case "Rating5": vm.Rating5Shortcut = shortcutText; break;
            case "CurationPicked": vm.CurationPickedShortcut = shortcutText; break;
            case "CurationRejected": vm.CurationRejectedShortcut = shortcutText; break;
            case "CurationNeutral": vm.CurationNeutralShortcut = shortcutText; break;
            case "CopyToEdit": vm.CopyToEditShortcut = shortcutText; break;
            case "CopyToPrint": vm.CopyToPrintShortcut = shortcutText; break;
        }
    }
}
