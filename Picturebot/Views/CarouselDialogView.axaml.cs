using System;
using Avalonia.Controls;
using Avalonia.Input;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.ViewModels;
using Serilog;

namespace Picturebot.Views;

public partial class CarouselDialogView : UserControl {
    public CarouselDialogView() {
        InitializeComponent();

        // Ensure the control can receive focus for keyboard events
        Focusable = true;

        // Focus once loaded
        Loaded += (s, e) => Focus();
    }

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
                // Fallback from D0-D9 to NumPad0-NumPad9
                if (gesture.Key >= Key.D0 && gesture.Key <= Key.D9) {
                    var offset = gesture.Key - Key.D0;
                    var equivalentNumPadKey = Key.NumPad0 + offset;
                    if (e.Key == equivalentNumPadKey) {
                        return true;
                    }
                }
                // Fallback from NumPad0-NumPad9 to D0-D9
                if (gesture.Key >= Key.NumPad0 && gesture.Key <= Key.NumPad9) {
                    var offset = gesture.Key - Key.NumPad0;
                    var equivalentDKey = Key.D0 + offset;
                    if (e.Key == equivalentDKey) {
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
        var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        var focusedElement = focusManager?.GetFocusedElement();
        if (focusedElement is TextBox || focusedElement is NumericUpDown || focusedElement is ComboBox) {
            return;
        }

        base.OnKeyDown(e);

        if (DataContext is not CarouselDialogViewModel vm) {
            return;
        }

        var settings = App.Services?.GetService<ISettingsService>()?.Current;
        if (settings != null) {
            if (MatchesGesture(e, settings.RedLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.Red);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.OrangeLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.Orange);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.YellowLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.Yellow);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.GreenLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.Green);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.BlueLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.Blue);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.PinkLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.Pink);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.PurpleLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.Purple);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.NoneLabelShortcut)) {
                vm.SetColorLabelCommand.Execute(ColorLabel.None);
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.Rating0Shortcut)) {
                vm.SetRatingCommand.Execute("0");
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.Rating1Shortcut)) {
                vm.SetRatingCommand.Execute("1");
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.Rating2Shortcut)) {
                vm.SetRatingCommand.Execute("2");
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.Rating3Shortcut)) {
                vm.SetRatingCommand.Execute("3");
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.Rating4Shortcut)) {
                vm.SetRatingCommand.Execute("4");
                e.Handled = true;
                return;
            }
            if (MatchesGesture(e, settings.Rating5Shortcut)) {
                vm.SetRatingCommand.Execute("5");
                e.Handled = true;
                return;
            }
        }

        Log.Debug("CarouselDialogView KeyDown: {Key}", e.Key);

        switch (e.Key) {
            case Key.Escape:
                vm.CloseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left:
                vm.PreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right:
                vm.NextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.P:
                vm.SetCurationStatusCommand.Execute(CurationStatus.Flagged);
                e.Handled = true;
                break;
            case Key.X:
                vm.SetCurationStatusCommand.Execute(CurationStatus.Rejected);
                e.Handled = true;
                break;
            case Key.U:
                vm.SetCurationStatusCommand.Execute(CurationStatus.Unflagged);
                e.Handled = true;
                break;
        }
    }
}
