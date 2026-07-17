namespace DeskDuck.Models;

/// <summary>
/// Groups all publisher service configurations under a single "Publishers" section
/// in appsettings.json so they can be bound together with <c>Configure&lt;PublishersSection&gt;</c>.
/// </summary>
public class PublishersSection
{
    /// <summary>Configuration for the system health monitor publisher.</summary>
    public SystemMonitorOptions SystemMonitor { get; set; } = new();

    /// <summary>Configuration for the weather update publisher.</summary>
    public WeatherPublisherOptions Weather { get; set; } = new();
}
