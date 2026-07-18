using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Core.Features.Chat;
using DeskDuck.Core.Features.Messaging;
using DeskDuck.Core.Features.Settings;
using DeskDuck.Core.Features.Shell;
using DeskDuck.Core.Features.SystemMonitor;
using DeskDuck.Core.Features.Weather;
using DeskDuck.Core.Manager;
using DeskDuck.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace DeskDuck.Core.Core;

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
        services.AddHttpClient("DeskDuck")
            .AddPolicyHandler(GetRetryPolicy());

        // Messenger
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Settings
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddTransient<SettingsViewModel>();

        // Shell
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IDuckMovementManager, DuckMovementManager>();

        // SystemMonitor
        services.AddSingleton<ISystemMetricsProvider, SystemMetricsProvider>();

        // Chat
        services.AddSingleton<IOllamaChatService, OllamaChatService>();
        services.AddTransient<ChatViewModel>();

        // Messaging
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddHostedService<RabbitMQBackgroundService>();

        // Background Publishers
        services.AddHostedService<SystemMonitorPublisherService>();
        services.AddHostedService<WeatherPublisherService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
