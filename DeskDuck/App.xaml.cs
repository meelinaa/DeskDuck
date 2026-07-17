using DeskDuck.Consumer;
using DeskDuck.Helper;
using DeskDuck.Models;
using DeskDuck.Publisher;
using DeskDuck.Services;
using DeskDuck.ViewModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using System.IO;
using System;

namespace DeskDuck
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// Entry point for the WinUI 3 application; creates and activates the main overlay window.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public IHost Host { get; }

        /// <summary>
        /// Initializes the singleton application object. This is the first authored code to
        /// execute, equivalent to main() or WinMain() in a traditional Win32 application.
        /// </summary>
        public App()
        {
            InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var settingsRepo = new SettingsRepository();
                    string configPath = settingsRepo.GetConfigPath();
                    config.SetBasePath(Path.GetDirectoryName(configPath)!);
                    config.AddJsonFile(Path.GetFileName(configPath), optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<SystemMonitorOptions>(context.Configuration.GetSection("Publishers:SystemMonitor"));
                    services.Configure<WeatherPublisherOptions>(context.Configuration.GetSection("Publishers:Weather"));
                    services.Configure<RabbitMqOptions>(context.Configuration.GetSection("RabbitMQ"));
                    services.Configure<OllamaOptions>(context.Configuration.GetSection("Ollama"));
                    services.Configure<DuckConfig>(context.Configuration.GetSection("Duck"));
                    services.Configure<GeneralSection>(context.Configuration.GetSection("General"));

                    services.AddHttpClient();
                    services.AddSingleton<RabbitMqPublisher>();
                    services.AddSingleton<IOllamaChatService, OllamaChatService>();
                    services.AddTransient<ChatViewModel>();
                    
                    services.AddSingleton<ISettingsRepository, SettingsRepository>();
                    services.AddTransient<SettingsViewModel>();

                    // MainWindow acts as the INotificationDispatcher. We register it as a singleton.
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<INotificationDispatcher>(sp => sp.GetRequiredService<MainWindow>());

                    services.AddHostedService<SystemMonitorPublisherService>();
                    services.AddHostedService<WeatherPublisherService>();
                    services.AddHostedService<RabbitMQBackgroundService>();
                })
                .Build();
        }

        /// <summary>
        /// Invoked when the application is launched. Creates the transparent overlay window
        /// and activates it so the duck appears on the desktop immediately.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Host.Start();
            _window = Host.Services.GetRequiredService<MainWindow>();
            _window.Activate();
        }
    }
}
