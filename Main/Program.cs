using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;
using AppRegistry.Infrastructure;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Main;

sealed class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        // 1. Setup Configuration
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .Build();
        
        var register = new FileSystemOrchestrator
        {
            Location = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Name = "Picturebot",
            Environment = AppEnvironment.Development
        };
        
        var state = await register.ExecuteAsync(null);
        
        // Log.Logger = new LoggerConfiguration()
        //     .ReadFrom.Configuration(configuration)
        //     .WriteTo.Console()
        //     .WriteTo.File(Path.Combine(logFolder, "log-.txt"), rollingInterval: RollingInterval.Day)
        //     .CreateLogger();

        try
        {
            Log.Information("Starting Picturebot in {Environment} mode", env);
            
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