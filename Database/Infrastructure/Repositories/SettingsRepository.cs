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
        var entity = await context.Settings.FirstOrDefaultAsync(s => s.Id == 1);
        
        if (entity == null) {
            entity = new Settings { Id = 1 };
            context.Settings.Add(entity);
        }

        _mapper.UpdateEntityFromModel(updatedSettings, entity);
        await context.SaveChangesAsync();
    }
}
