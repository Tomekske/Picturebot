using System;
using Avalonia.Controls;
using Avalonia.Input;
using Domain.Enums;
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
            case Key.D1:
            case Key.NumPad1:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.Red);
                } else {
                    vm.SetRatingCommand.Execute("1");
                }
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.Orange);
                } else {
                    vm.SetRatingCommand.Execute("2");
                }
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.Yellow);
                } else {
                    vm.SetRatingCommand.Execute("3");
                }
                e.Handled = true;
                break;
            case Key.D4:
            case Key.NumPad4:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.Green);
                } else {
                    vm.SetRatingCommand.Execute("4");
                }
                e.Handled = true;
                break;
            case Key.D5:
            case Key.NumPad5:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.Blue);
                } else {
                    vm.SetRatingCommand.Execute("5");
                }
                e.Handled = true;
                break;
            case Key.D6:
            case Key.NumPad6:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.Pink);
                    e.Handled = true;
                }
                break;
            case Key.D7:
            case Key.NumPad7:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.Purple);
                    e.Handled = true;
                }
                break;
            case Key.D0:
            case Key.NumPad0:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                    vm.SetColorLabelCommand.Execute(ColorLabel.None);
                } else {
                    vm.SetRatingCommand.Execute("0");
                }
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
