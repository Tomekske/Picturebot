using Database.Domain.Entities;
using Domain.Models;
using Riok.Mapperly.Abstractions;

namespace Database.Infrastructure.Mappers;

[Mapper]
public partial class SettingsMapper
{
    [MapperIgnoreSource(nameof(Settings.Id))]
    public partial SettingsModel EntityToModel(Settings entity);

    [MapperIgnoreTarget(nameof(Settings.Id))]
    public partial void UpdateEntityFromModel(SettingsModel model, Settings entity);
}
