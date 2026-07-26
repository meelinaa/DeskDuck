using DeskDuck.Core.Models;
using DeskDuck.Core.Core;
using DeskDuck.Features.Shell;
using DeskDuck.Core.Features.Chat;
using DeskDuck.Core.Features.Weather;
using DeskDuck.Core.Features.SystemMonitor;
using DeskDuck.Core.Features.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using System;
using System.IO;
using DeskDuck.Core.Features.Settings;
using DeskDuck.Core.Features.Shell;

namespace DeskDuck
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// Entry point for the WinUI 3 application; creates and activates the main overlay window.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Gets the configured application host that manages dependency injection, logging, and background services.
        /// </summary>
        public IHost Host { get; }

        /// <summary>
        /// Initializes the singleton application object. This is the first authored code to
        /// execute, equivalent to main() or WinMain() in a traditional Win32 application.
        /// </summary>
        public App()
        {
            InitializeComponent();

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskDuck", "logs", "deskduck.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var settingsRepo = new SettingsRepository(Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsRepository>.Instance);
                    string configPath = settingsRepo.GetConfigPath();
                    config.SetBasePath(Path.GetDirectoryName(configPath)!);
                    config.AddJsonFile(Path.GetFileName(configPath), optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddDeskDuckFeatures(context.Configuration);
                    services.AddSingleton<IDuckWindowManager, DuckWindowManager>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        /// <summary>
        /// Invoked when the application is launched. Creates the transparent overlay window
        /// and activates it so the duck appears on the desktop immediately.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Host.Start();
            _window = Host.Services.GetRequiredService<MainWindow>();
            _window.Activate();
        }
    }
}
