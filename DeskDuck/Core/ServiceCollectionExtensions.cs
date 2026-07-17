using DeskDuck.Models;
using DeskDuck.Features.Chat;
using DeskDuck.Features.Weather;
using DeskDuck.Features.SystemMonitor;
using DeskDuck.Features.Settings;
using DeskDuck.Features.Messaging;
using DeskDuck.Features.Shell;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeskDuck.Core
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDeskDuckFeatures(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure Options
            services.Configure<SystemMonitorOptions>(configuration.GetSection("Publishers:SystemMonitor"));
            services.Configure<WeatherPublisherOptions>(configuration.GetSection("Publishers:Weather"));
            services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
            services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));
            services.Configure<DuckConfig>(configuration.GetSection("Duck"));
            services.Configure<GeneralSection>(configuration.GetSection("General"));

            // Core
            services.AddHttpClient();

            // Settings
            services.AddSingleton<ISettingsRepository, SettingsRepository>();
            services.AddTransient<SettingsViewModel>();

            // Shell
            services.AddSingleton<MainWindow>();

            // Chat
            services.AddSingleton<IOllamaChatService, OllamaChatService>();
            services.AddTransient<ChatViewModel>();

            // Messaging
            services.AddSingleton<RabbitMqPublisher>();
            services.AddHostedService<RabbitMQBackgroundService>();

            // Background Publishers
            services.AddHostedService<SystemMonitorPublisherService>();
            services.AddHostedService<WeatherPublisherService>();

            return services;
        }
    }
}
