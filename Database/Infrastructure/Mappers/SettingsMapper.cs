using Database.Domain.Entities;
using Domain.Models;
using Riok.Mapperly.Abstractions;

namespace Database.Infrastructure.Mappers;

[Mapper]
public partial class SettingsMapper {
    [MapperIgnoreSource(nameof(Settings.Id))]
    [MapperIgnoreSource(nameof(Settings.MasterTagsJson))]
    [MapperIgnoreSource(nameof(Settings.HierarchyNodesJson))]
    [MapperIgnoreSource(nameof(Settings.TagGroupsJson))]
    [MapperIgnoreTarget(nameof(SettingsModel.MasterTags))]
    [MapperIgnoreTarget(nameof(SettingsModel.HierarchyNodes))]
    [MapperIgnoreTarget(nameof(SettingsModel.TagGroups))]
    public partial SettingsModel EntityToModel(Settings entity);

    [MapperIgnoreTarget(nameof(Settings.Id))]
    [MapperIgnoreTarget(nameof(Settings.MasterTagsJson))]
    [MapperIgnoreTarget(nameof(Settings.HierarchyNodesJson))]
    [MapperIgnoreTarget(nameof(Settings.TagGroupsJson))]
    [MapperIgnoreSource(nameof(SettingsModel.MasterTags))]
    [MapperIgnoreSource(nameof(SettingsModel.HierarchyNodes))]
    [MapperIgnoreSource(nameof(SettingsModel.TagGroups))]
    public partial void UpdateEntityFromModel(SettingsModel model, Settings entity);
}
