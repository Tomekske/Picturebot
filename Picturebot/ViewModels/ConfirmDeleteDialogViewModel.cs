using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Picturebot.Views;

namespace Picturebot.ViewModels;

public partial class ConfirmDeleteDialogViewModel : ViewModelBase {
    private readonly Action<bool> _onResult;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _message;

    public ConfirmDeleteDialogViewModel(string title, string message, Action<bool> onResult) {
        Title = title;
        Message = message;
        _onResult = onResult;
    }

    [RelayCommand]
    private void Confirm() {
        _onResult(true);
        MainWindow.DialogManager.DismissDialog();
    }

    [RelayCommand]
    private void Cancel() {
        _onResult(false);
        MainWindow.DialogManager.DismissDialog();
    }
}
