using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Database.Domain.Interfaces;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;
using SukiUI;

using Microsoft.Extensions.DependencyInjection;

namespace Picturebot.Services;

public class SettingsService : ISettingsService {
    private readonly IServiceScopeFactory _scopeFactory;
    private SettingsModel _current = new();

    public SettingsService(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
    }

    public SettingsModel Current {
        get => _current;
        private set {
            if (Equals(value, _current)) {
                return;
            }

            _current = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync() {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        var settings = await repository.LoadAsync();
        Current = settings;
        ApplyTheme(settings.ThemeMode);
    }

    public async Task UpdateAsync(SettingsModel settings) {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        await repository.UpdateAsync(settings);
        Current = settings;
        ApplyTheme(settings.ThemeMode);
    }

    private void ApplyTheme(ThemeMode mode) {
        try {
            switch (mode) {
                case ThemeMode.Light:
                    SukiTheme.GetInstance().ChangeBaseTheme(ThemeVariant.Light);
                    break;
                case ThemeMode.Dark:
                    SukiTheme.GetInstance().ChangeBaseTheme(ThemeVariant.Dark);
                    break;
                case ThemeMode.System:
                    if (Application.Current != null) {
                        Application.Current.RequestedThemeVariant = ThemeVariant.Default;
                    }

                    break;
            }
        } catch {
            // Log or ignore
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
