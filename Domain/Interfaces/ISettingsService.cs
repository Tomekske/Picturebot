using Domain.Models;
using System.ComponentModel;

namespace Domain.Interfaces;

public interface ISettingsService : INotifyPropertyChanged {
    SettingsModel Current { get; }
    Task InitializeAsync();
    Task UpdateAsync(SettingsModel settings);
}
