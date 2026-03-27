using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Database.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Database.Infrastructure;

public static class DependencyInjection {
    public static IServiceCollection AddDatabaseLayer(this IServiceCollection services, string connectionString) {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString)
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<INodeRepository, NodeRepository>();

        return services;
    }
}
