namespace DeskDuck.Features.Weather
{
    /// <summary>
    /// Configuration options for the weather publisher service.
    /// Maps to the "Publishers:Weather" section of appsettings.json and
    /// is injected via <c>IOptions&lt;WeatherPublisherOptions&gt;</c>.
    /// </summary>
    public class WeatherPublisherOptions
    {
        /// <summary>Whether the weather publisher is active. Set to <c>false</c> to disable updates.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>How often (in minutes) the service fetches and publishes a weather update.</summary>
        public int IntervalMinutes { get; set; } = 30;

        /// <summary>OpenWeatherMap API key. The service skips updates when this is empty.</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// When non-empty, overrides the auto-detected city with the specified name.
        /// Useful for users whose ISP assigns IPs in a different city.
        /// </summary>
        public string OverrideCity { get; set; } = "Berlin";
    }
}
