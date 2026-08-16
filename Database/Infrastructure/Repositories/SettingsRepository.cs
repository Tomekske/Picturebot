using System;
using System.Collections.Generic;
using System.Text.Json;
using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Database.Infrastructure.Mappers;
using Domain.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Repositories;

/// <summary>
///     Implements the data access logic for application settings using Entity Framework Core.
/// </summary>
public class SettingsRepository(ApplicationDbContext context) : ISettingsRepository {
    private readonly SettingsMapper _mapper = new();

    public async Task<SettingsModel> LoadAsync() {
        try {
            var entity = await context.Settings.FirstOrDefaultAsync(s => s.Id == 1);

            if (entity != null) {
                var model = _mapper.EntityToModel(entity);
                PopulateModelFromEntity(entity, model);
                return model;
            }

            entity = new Settings { Id = 1 };
            context.Settings.Add(entity);
            await context.SaveChangesAsync();

            var newModel = _mapper.EntityToModel(entity);
            PopulateModelFromEntity(entity, newModel);
            return newModel;
        } catch (Exception ex) when (ex.InnerException is SqliteException or SqliteException) {
            Console.WriteLine($"Database schema mismatch detected while loading settings. Using defaults. Error: {ex.Message}");
            return new SettingsModel();
        }
    }

    public async Task UpdateAsync(SettingsModel updatedSettings) {
        var entity = await context.Settings.FirstOrDefaultAsync(s => s.Id == 1);

        if (entity == null) {
            entity = new Settings { Id = 1 };
            context.Settings.Add(entity);
        }

        _mapper.UpdateEntityFromModel(updatedSettings, entity);
        PopulateEntityFromModel(updatedSettings, entity);
        await context.SaveChangesAsync();
    }

    private static void PopulateModelFromEntity(Settings entity, SettingsModel model) {
        try {
            if (!string.IsNullOrWhiteSpace(entity.MasterTagsJson) && entity.MasterTagsJson != "[]") {
                model.MasterTags = JsonSerializer.Deserialize<List<Tag>>(entity.MasterTagsJson) ?? new();
            }
            if (!string.IsNullOrWhiteSpace(entity.HierarchyNodesJson) && entity.HierarchyNodesJson != "[]") {
                model.HierarchyNodes = JsonSerializer.Deserialize<List<HierarchyNode>>(entity.HierarchyNodesJson) ?? new();
            }
            if (!string.IsNullOrWhiteSpace(entity.TagGroupsJson) && entity.TagGroupsJson != "[]") {
                model.TagGroups = JsonSerializer.Deserialize<List<TagGroup>>(entity.TagGroupsJson) ?? new();
            }
            if (Guid.TryParse(entity.ActiveTagGroupId, out var groupId)) {
                model.ActiveTagGroupId = groupId;
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error deserializing settings tag architecture: {ex.Message}");
        }
    }

    private static void PopulateEntityFromModel(SettingsModel model, Settings entity) {
        try {
            entity.MasterTagsJson = JsonSerializer.Serialize(model.MasterTags);
            entity.HierarchyNodesJson = JsonSerializer.Serialize(model.HierarchyNodes);
            entity.TagGroupsJson = JsonSerializer.Serialize(model.TagGroups);
            entity.ActiveTagGroupId = model.ActiveTagGroupId?.ToString();
        } catch (Exception ex) {
            Console.WriteLine($"Error serializing settings tag architecture: {ex.Message}");
        }
    }
}
