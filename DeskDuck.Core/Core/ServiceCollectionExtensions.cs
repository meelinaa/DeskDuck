using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Core.Features.Chat;
using DeskDuck.Core.Features.Messaging;
using DeskDuck.Core.Features.Movement;
using DeskDuck.Core.Features.Settings;
using DeskDuck.Core.Features.Shell;
using DeskDuck.Core.Features.SystemMonitor;
using DeskDuck.Core.Features.Weather;
using DeskDuck.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace DeskDuck.Core.Core;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register DeskDuck specific services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all required features, options, and services for the DeskDuck application into the dependency injection container.
    /// This includes background services, ViewModels, external clients, and messaging infrastructure.
    /// </summary>
    /// <param name="services">The service collection to add the features to.</param>
    /// <param name="configuration">The application configuration used to bind options.</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
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
        services.AddSingleton<IDuckMovementController, DuckMovementController>();

        // SystemMonitor
        services.AddSingleton<ISystemMetricsProvider, SystemMetricsProvider>();

        // Chat
        services.AddSingleton<IOllamaChatService, OllamaChatService>();
        services.AddTransient<ChatViewModel>();

        // Messaging
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddHostedService<RabbitMqBackgroundService>();

        // Background Publishers
        services.AddHostedService<SystemMonitorPublisherService>();
        services.AddHostedService<WeatherPublisherService>();

        return services;
    }

    /// <summary>
    /// Creates a Polly retry policy for HTTP clients to automatically retry failed requests.
    /// This ensures robustness against transient network errors when communicating with external APIs.
    /// </summary>
    /// <returns>An asynchronous retry policy for HTTP responses.</returns>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
