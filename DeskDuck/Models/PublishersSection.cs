namespace DeskDuck.Models;

public class PublishersSection
{
    public SystemMonitorOptions SystemMonitor { get; set; } = new();
    public WeatherPublisherOptions Weather { get; set; } = new();
}
