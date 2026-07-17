using DeskDuck.Models;
using DeskDuck.Publisher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeskDuck.Services
{
    /// <summary>
    /// Hosted background service that periodically fetches the current weather
    /// from the OpenWeatherMap API and publishes it as an informational notification
    /// to RabbitMQ. When no city is explicitly configured, the user's location is
    /// auto-detected via the ip-api.com geolocation endpoint.
    /// </summary>
    public partial class WeatherPublisherService(
        IOptions<WeatherPublisherOptions> options,
        RabbitMqPublisher publisher,
        IHttpClientFactory httpClientFactory) : BackgroundService
    {
        private readonly IOptions<WeatherPublisherOptions> _options = options;
        private readonly RabbitMqPublisher _publisher = publisher;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        /// <summary>
        /// Main service loop. Reads the latest configuration on every iteration so that
        /// interval and API key changes take effect without restarting the application.
        /// Waits for the configured interval (minimum 1 minute) between weather updates.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                WeatherPublisherOptions config = _options.Value;
                if (config.Enabled)
                {
                    try
                    {
                        await PublishWeatherUpdateAsync(config, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WeatherPublisher] Error publishing weather: {ex.Message}");
                    }
                }

                int intervalMinutes = Math.Max(1, config.IntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

        /// <summary>
        /// Fetches the current weather for the target city and publishes a formatted
        /// notification to RabbitMQ. If no city is configured, the method calls ip-api.com
        /// to determine the user's city from their public IP address. Returns early if the
        /// API key is missing or the city cannot be determined.
        /// </summary>
        private async Task PublishWeatherUpdateAsync(WeatherPublisherOptions config, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                Debug.WriteLine("[WeatherPublisher] OpenWeatherMap ApiKey is empty. Skipping weather update.");
                return;
            }

            string city = config.OverrideCity;
            if (string.IsNullOrWhiteSpace(city))
            {
                city = "Berlin";
            }
            try
            {
                string url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={config.ApiKey}&units=metric&lang=de";
                var httpClient = _httpClientFactory.CreateClient();
                string response = await httpClient.GetStringAsync(url, cancellationToken);

                using JsonDocument doc = JsonDocument.Parse(response);
                JsonElement root = doc.RootElement;

                double temp = 0;
                if (root.TryGetProperty("main", out JsonElement mainProp) && mainProp.TryGetProperty("temp", out JsonElement tempProp))
                {
                    temp = tempProp.GetDouble();
                }

                string description = "Unbekannt";
                if (root.TryGetProperty("weather", out JsonElement weatherProp) && weatherProp.ValueKind == JsonValueKind.Array && weatherProp.GetArrayLength() > 0)
                {
                    JsonElement firstWeather = weatherProp[0];
                    if (firstWeather.TryGetProperty("description", out JsonElement descProp))
                    {
                        description = descProp.GetString() ?? "Unbekannt";
                    }
                }

                string weatherText = $"Aktuelles Wetter in {city}: {temp:F1}°C, {description}.";

                await _publisher.PublishAsync(
                    source: "Wetter",
                    severity: "Info",
                    text: weatherText,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WeatherPublisher] Error fetching weather from API: {ex.Message}");
            }
        }

        // IHttpClientFactory manages client lifetimes. Base class dispose will clean up BackgroundService.
    }
}
