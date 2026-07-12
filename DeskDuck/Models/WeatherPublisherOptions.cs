namespace DeskDuck.Models
{
    public class WeatherPublisherOptions
    {
        public bool Enabled { get; set; } = true;
        public int IntervalMinutes { get; set; } = 30;
        public string ApiKey { get; set; } = string.Empty;
        public string Location { get; set; } = "auto";
        public string OverrideCity { get; set; } = string.Empty;
    }
}
