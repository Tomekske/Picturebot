using Domain.Models;

namespace Database.Domain.Interfaces;

/// <summary>
///     Defines the data access contract for managing application settings.
/// </summary>
public interface ISettingsRepository {
    Task<SettingsModel> LoadAsync();

    Task UpdateAsync(SettingsModel settings);
}
