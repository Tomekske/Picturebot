using Avalonia.Controls;
using Main.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Domain.Interfaces;

namespace Main.Views;

public partial class SettingsDialog : UserControl {
    public SettingsDialog() {
        InitializeComponent();
        var service = App.Services?.GetRequiredService<ISettingsService>();
        
        DataContext = service != null ? new SettingsDialogViewModel(service) : new SettingsDialogViewModel();
    }
}
