using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Database.Infrastructure.Mappers;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Repositories;

/// <summary>
///     Implements the data access logic for application settings using Entity Framework Core.
/// </summary>
public class SettingsRepository(ApplicationDbContext context) : ISettingsRepository {
    private readonly SettingsMapper _mapper = new();

    public async Task<SettingsModel> LoadAsync() {
        var entity = await context.Settings.FirstOrDefaultAsync(s => s.Id == 1);

        if (entity != null) {
            return _mapper.EntityToModel(entity);
        }

        entity = new Settings { Id = 1 };
        context.Settings.Add(entity);
        await context.SaveChangesAsync();

        return _mapper.EntityToModel(entity);
    }

    public async Task UpdateAsync(SettingsModel updatedSettings) {
        // Re-fetch to ensure we are updating the tracked entity
        var currentSettings = await LoadAsync();

        currentSettings.ThemeMode = updatedSettings.ThemeMode;
        currentSettings.LibraryPath = updatedSettings.LibraryPath;
        currentSettings.GroupingThreshold = updatedSettings.GroupingThreshold;
        currentSettings.LaunchMaximized = updatedSettings.LaunchMaximized;

        await context.SaveChangesAsync();
    }
}
