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

namespace Main.Services;

public class SettingsService : ISettingsService {
    private readonly ISettingsRepository _repository;
    private SettingsModel _current = new();

    public SettingsModel Current {
        get => _current;
        private set {
            if (Equals(value, _current)) return;
            _current = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsService(ISettingsRepository repository) {
        _repository = repository;
    }

    public async Task InitializeAsync() {
        var settings = await _repository.LoadAsync();
        Current = settings;
        ApplyTheme(settings.ThemeMode);
    }

    public async Task UpdateAsync(SettingsModel settings) {
        await _repository.UpdateAsync(settings);
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
