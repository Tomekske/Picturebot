using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Main.ViewModels;
using Main.Views;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Main;

public partial class App : Application {
    public static IServiceProvider? Services { get; set; }
    public static IConfiguration? Configuration { get; private set; }

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);

        // Setup Configuration (can also be called from Program.cs)
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? AppEnvironment.Production;
        
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false)
            .AddJsonFile($"appsettings.{env}.json", true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Environment", env } })
            .Build();
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // Initialize Settings
            var settingsService = Services?.GetRequiredService<ISettingsService>();
            settingsService?.InitializeAsync().GetAwaiter().GetResult();

            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var mainWindow = new MainWindow {
                DataContext = Services?.GetRequiredService<MainWindowViewModel>(),
            };

            if (settingsService?.Current.LaunchMaximized == true) {
                mainWindow.WindowState = WindowState.Maximized;
            }

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation() {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
