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
    public partial class WeatherPublisherService(
        IOptions<WeatherPublisherOptions> options,
        RabbitMqPublisher publisher) : BackgroundService
    {
        private readonly IOptions<WeatherPublisherOptions> _options = options;
        private readonly RabbitMqPublisher _publisher = publisher;
        private readonly HttpClient _httpClient = new();

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

                // Wait for the configured interval
                int intervalMinutes = Math.Max(1, config.IntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

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
                // Auto-detect city via ip-api
                try
                {
                    string geoResponse = await _httpClient.GetStringAsync("http://ip-api.com/json/", cancellationToken);
                    using JsonDocument doc = JsonDocument.Parse(geoResponse);
                    if (doc.RootElement.TryGetProperty("city", out var cityProp))
                    {
                        city = cityProp.GetString() ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WeatherPublisher] Error detecting location: {ex.Message}");
                    // Fallback to auto-detect failed
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                Debug.WriteLine("[WeatherPublisher] Location/city could not be determined.");
                return;
            }

            // Fetch weather from OpenWeatherMap
            try
            {
                string url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={config.ApiKey}&units=metric&lang=de";
                string response = await _httpClient.GetStringAsync(url, cancellationToken);

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

        public override void Dispose()
        {
            _httpClient.Dispose();
            base.Dispose();
        }
    }
}
