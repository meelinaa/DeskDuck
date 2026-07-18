using DeskDuck.Models;
using DeskDuck.Features.Chat;
using DeskDuck.Features.Weather;
using DeskDuck.Features.SystemMonitor;
using DeskDuck.Features.Settings;
using DeskDuck.Features.Messaging;
using DeskDuck.Features.Shell;
using DeskDuck.Manager;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Net.Http;

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
}
