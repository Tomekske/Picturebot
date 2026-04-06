using Database.Domain.Interfaces;
using Database.Domain.Services;
using Database.Infrastructure.Data;
using Database.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Database.Infrastructure;

public static class DependencyInjection {
    public static IServiceCollection AddDatabaseLayer(this IServiceCollection services, string connectionString) {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString)
                .ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<INodeRepository, NodeRepository>();
        services.AddScoped<IPictureRepository, PictureRepository>();
        services.AddTransient<IPictureGroupingService, PictureGroupingService>();

        return services;
    }
}
