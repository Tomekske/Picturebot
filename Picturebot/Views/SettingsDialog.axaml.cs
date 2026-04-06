using Avalonia.Controls;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Picturebot.ViewModels;

namespace Picturebot.Views;

public partial class SettingsDialog : UserControl {
    public SettingsDialog() {
        InitializeComponent();
        var service = App.Services?.GetRequiredService<ISettingsService>();

        DataContext = service != null ? new SettingsDialogViewModel(service) : new SettingsDialogViewModel();
    }
}
