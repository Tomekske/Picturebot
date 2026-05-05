using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using AppRegistry.Infrastructure;
using Avalonia;
using Database.Infrastructure;
using Database.Infrastructure.Data;
using Domain.Enums;
using Domain.Interfaces;
using Graph.Domain.Interfaces;
using Graph.Domain.Strategies;
using Graph.Infrastructure.Commands;
using Graph.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Picturebot.Services;
using Picturebot.ViewModels;
using PictureWorker.Domain.Interfaces;
using PictureWorker.Infrastructure.Services;
using Serilog;
using Synchronize;

namespace Picturebot;

internal sealed class Program {
    [STAThread]
    public static void Main(string[] args) {
        try {
            // Setup Configuration
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? AppEnvironment.Production;
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile($"appsettings.{env}.json", true)
                .AddInMemoryCollection(new Dictionary<string, string?> { { "Environment", env } })
                .Build();

            var register = new FileSystemOrchestrator {
                Location = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Name = "Picturebot",
                Environment = env
            };

            var response = register.ExecuteAsync(new object()).GetAwaiter().GetResult();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.File(Path.Combine(response.Value, "Logs", "log-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            if (response.IsError) {
                Log.Error("Failed to initialize FileSystem: {Error}", response.FirstError.Description);
                return;
            }

            Log.Information("Starting Picturebot in {Env} mode", env);
            Log.Debug("Application Directory: {AppDir}", response.Value);

            // Database Migrations
            var connectionString = $"Data Source={Path.Combine(response.Value, "picturebot.db")}";
            Log.Information("Applying database migrations at: {DbPath}", connectionString);

            // Setup DI
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog());
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDatabaseLayer(connectionString);
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddScoped<IPathService, PathService>();
            services.AddSingleton<IPictureAnalyzer, PictureAnalyzerService>();
            services.AddSingleton<IPictureProcessor, PictureProcessorService>();
            services.AddSingleton<IPickedService, PickedService>();
            services.AddSingleton<ICurationQueue, CurationQueue>();
            services.AddHostedService<PictureWorkerService>();

            // Graph Services
            services.AddScoped<INodeService, NodeService>();
            services.AddScoped<IFolderService, FolderService>();
            services.AddScoped<IAlbumService, AlbumService>();
            services.AddScoped<IImportAlbumsService, ImportAlbumsService>();
            services.AddTransient<IImportPicturesCommand, ImportPicturesCommand>();
            services.AddScoped<NodeStrategyFactory>();
            services.AddScoped<FolderCreationStrategy>();
            services.AddScoped<AlbumCreationStrategy>();
            services.AddScoped<PictureCreationStrategy>();

            // ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<GalleryViewModel>();
            services.AddTransient<NavigationPaneViewModel>();
            services.AddTransient<DetailsInspectorViewModel>();
            services.AddTransient<StatusBarViewModel>();

            App.Services = services.BuildServiceProvider();

            using (var scope = App.Services.CreateScope()) {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Database.Migrate(); // Use sync Migrate() if possible, or wait
            }

            Log.Information("Database migrations applied successfully");

            // Manually start hosted services
            var hostedServices = App.Services.GetServices<IHostedService>();
            foreach (var hostedService in hostedServices) {
                hostedService.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            // Manually stop hosted services
            foreach (var hostedService in hostedServices) {
                hostedService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        } catch (Exception ex) {
            Log.Fatal(ex, "Application terminated unexpectedly");
        } finally {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
