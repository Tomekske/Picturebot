using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
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
            services.AddScoped<IXmpService, XmpService>();
            services.AddScoped<ICopyService, CopyService>();
            services.AddSingleton<IPictureAnalyzer, PictureAnalyzerService>();
            services.AddSingleton<IPictureProcessor, PictureProcessorService>();
            services.AddSingleton<IImageEmbeddingService, ImageEmbeddingService>();
            services.AddScoped<ITaxonomyService, TaxonomyService>();
            services.AddSingleton<IGlobalExemplarCentroidService, GlobalExemplarCentroidService>();
            services.AddScoped<IFewShotTagDiscoveryService, FewShotTagDiscoveryService>();
            services.AddSingleton<IPickedService, PickedService>();
            services.AddSingleton<CurationQueue>();
            services.AddSingleton<ICurationQueue>(sp => sp.GetRequiredService<CurationQueue>());
            services.AddHostedService(sp => sp.GetRequiredService<CurationQueue>());
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

                var settings = context.Settings.FirstOrDefault();
                if (settings != null) {
                    bool modified = false;
                    if (settings.RedLabelShortcut == "Ctrl+D1") { settings.RedLabelShortcut = "Ctrl+NumPad1"; modified = true; }
                    if (settings.OrangeLabelShortcut == "Ctrl+D2") { settings.OrangeLabelShortcut = "Ctrl+NumPad2"; modified = true; }
                    if (settings.YellowLabelShortcut == "Ctrl+D3") { settings.YellowLabelShortcut = "Ctrl+NumPad3"; modified = true; }
                    if (settings.GreenLabelShortcut == "Ctrl+D4") { settings.GreenLabelShortcut = "Ctrl+NumPad4"; modified = true; }
                    if (settings.BlueLabelShortcut == "Ctrl+D5") { settings.BlueLabelShortcut = "Ctrl+NumPad5"; modified = true; }
                    if (settings.PinkLabelShortcut == "Ctrl+D6") { settings.PinkLabelShortcut = "Ctrl+NumPad6"; modified = true; }
                    if (settings.PurpleLabelShortcut == "Ctrl+D7") { settings.PurpleLabelShortcut = "Ctrl+NumPad7"; modified = true; }
                    if (settings.NoneLabelShortcut == "Ctrl+D0") { settings.NoneLabelShortcut = "Ctrl+NumPad0"; modified = true; }
                    if (settings.Rating0Shortcut == "D0") { settings.Rating0Shortcut = "NumPad0"; modified = true; }
                    if (settings.Rating1Shortcut == "D1") { settings.Rating1Shortcut = "NumPad1"; modified = true; }
                    if (settings.Rating2Shortcut == "D2") { settings.Rating2Shortcut = "NumPad2"; modified = true; }
                    if (settings.Rating3Shortcut == "D3") { settings.Rating3Shortcut = "NumPad3"; modified = true; }
                    if (settings.Rating4Shortcut == "D4") { settings.Rating4Shortcut = "NumPad4"; modified = true; }
                    if (settings.Rating5Shortcut == "D5") { settings.Rating5Shortcut = "NumPad5"; modified = true; }
                    if (string.IsNullOrEmpty(settings.CopyToEditShortcut)) { settings.CopyToEditShortcut = "Ctrl+E"; modified = true; }
                    if (string.IsNullOrEmpty(settings.CopyToPrintShortcut)) { settings.CopyToPrintShortcut = "Shift+E"; modified = true; }
                    if (modified) {
                        context.SaveChanges();
                    }
                }
            }

            Log.Information("Database migrations applied successfully");

            // Manually start hosted services
            var hostedServices = App.Services.GetServices<IHostedService>();
            foreach (var hostedService in hostedServices) {
                hostedService.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            // Clear the SynchronizationContext to prevent deadlocks when calling StopAsync synchronously on the UI thread
            SynchronizationContext.SetSynchronizationContext(null);

            // Manually stop hosted services on the ThreadPool with a 3-second timeout
            Task.Run(async () => {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3))) {
                    foreach (var hostedService in hostedServices) {
                        try {
                            await hostedService.StopAsync(cts.Token);
                        } catch (Exception ex) {
                            Log.Warning(ex, "Timeout or error stopping hosted service {Service}", hostedService.GetType().Name);
                        }
                    }
                }
            }).GetAwaiter().GetResult();

            Environment.Exit(0);
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
