using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DeskDuck
{
    public class WeatherPublisherService : BackgroundService
    {
        private readonly IOptions<WeatherPublisherOptions> _options;
        private readonly RabbitMqPublisher _publisher;
        private readonly HttpClient _httpClient;

        public WeatherPublisherService(
            IOptions<WeatherPublisherOptions> options,
            RabbitMqPublisher publisher)
        {
            _options = options;
            _publisher = publisher;
            _httpClient = new HttpClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var config = _options.Value;
                if (config.Enabled)
                {
                    try
                    {
                        await PublishWeatherUpdateAsync(config, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WeatherPublisher] Error publishing weather: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("[WeatherPublisher] OpenWeatherMap ApiKey is empty. Skipping weather update.");
                return;
            }

            string city = config.OverrideCity;
            if (string.IsNullOrWhiteSpace(city))
            {
                // Auto-detect city via ip-api
                try
                {
                    var geoResponse = await _httpClient.GetStringAsync("http://ip-api.com/json/", cancellationToken);
                    using var doc = JsonDocument.Parse(geoResponse);
                    if (doc.RootElement.TryGetProperty("city", out var cityProp))
                    {
                        city = cityProp.GetString() ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WeatherPublisher] Error detecting location: {ex.Message}");
                    // Fallback to auto-detect failed
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                System.Diagnostics.Debug.WriteLine("[WeatherPublisher] Location/city could not be determined.");
                return;
            }

            // Fetch weather from OpenWeatherMap
            try
            {
                string url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={config.ApiKey}&units=metric&lang=de";
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                double temp = 0;
                if (root.TryGetProperty("main", out var mainProp) && mainProp.TryGetProperty("temp", out var tempProp))
                {
                    temp = tempProp.GetDouble();
                }

                string description = "Unbekannt";
                if (root.TryGetProperty("weather", out var weatherProp) && weatherProp.ValueKind == JsonValueKind.Array && weatherProp.GetArrayLength() > 0)
                {
                    var firstWeather = weatherProp[0];
                    if (firstWeather.TryGetProperty("description", out var descProp))
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
                System.Diagnostics.Debug.WriteLine($"[WeatherPublisher] Error fetching weather from API: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            _httpClient.Dispose();
            base.Dispose();
        }
    }
}
