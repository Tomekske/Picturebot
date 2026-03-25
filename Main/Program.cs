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
            .WriteTo.File(Path.Combine(response.Value, "log-.txt"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        if (response.IsError)
        {
            Log.Error("Failed to initialize FileSystem: {Error}", response.FirstError.Description);
        }

        try
        {
            Log.Information("Starting Picturebot in {Env} mode", env);
            Log.Debug("Application Directory: {AppDir}", response.Value);
            
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