using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;
using AppRegistry.Infrastructure;
using Database.Infrastructure.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Main;

sealed class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        // Setup Configuration
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? AppEnvironment.Production;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .Build();
        
        var register = new FileSystemOrchestrator
        {
            Location = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Name = "Picturebot",
            Environment = env
        };
        
        var response  = await register.ExecuteAsync(null);
        
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.File(Path.Combine(response.Value, "Logs", "log-.txt"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        if (response.IsError)
        {
            Log.Error("Failed to initialize FileSystem: {Error}", response.FirstError.Description);
            return;
        }

        try 
        {
            Log.Information("Starting Picturebot in {Env} mode", env);
            Log.Debug("Application Directory: {AppDir}", response.Value);

            // Database Migrations
            var connectionString = $"Data Source={Path.Combine(response.Value, "picturebot.db")}";
            Log.Information("Applying database migrations at: {DbPath}", connectionString);

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlite(connectionString)
                          .UseSnakeCaseNamingConvention()
                          .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                await context.Database.MigrateAsync();
            }

            Log.Information("Database migrations applied successfully");
            
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}