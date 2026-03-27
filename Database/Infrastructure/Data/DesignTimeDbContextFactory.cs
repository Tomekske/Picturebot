using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Database.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext> {
    public ApplicationDbContext CreateDbContext(string[] args) {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"); // ?? "Development";

        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "Main");

        // If the 'Main' folder is not found directly, try the parent folder (e.g., if running from the Database project folder)
        if (!Directory.Exists(configPath)) {
            configPath = Path.Combine(currentDir, "..", "Main");
        }

        Console.WriteLine($"[EF Design-Time] Execution Directory: {currentDir}");
        Console.WriteLine($"[EF Design-Time] Target Config Path: {Path.GetFullPath(configPath)}");
        Console.WriteLine($"[EF Design-Time] Active Environment: {environment}");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetFullPath(configPath))
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile($"appsettings.{environment}.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        var appDataPath = configuration["Picturebot:AppDataPath"];
        Console.WriteLine($"[EF Design-Time] Resolved AppDataPath: {appDataPath ?? "MISSING"}");

        if (string.IsNullOrEmpty(appDataPath)) {
            throw new InvalidOperationException(
                $"Configuration key 'Picturebot:AppDataPath' not found for environment '{environment}' in {Path.GetFullPath(configPath)}.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite(DbPathProvider.GetConnectionString(configuration));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
