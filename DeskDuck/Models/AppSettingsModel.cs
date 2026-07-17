namespace DeskDuck.Models;

/// <summary>
/// Root configuration model that maps to the top-level structure of appsettings.json.
/// Holds all application settings grouped by feature area.
/// </summary>
public class AppSettingsModel
{
    /// <summary>Settings for all configurable publisher services.</summary>
    public PublishersSection Publishers { get; set; } = new();

    /// <summary>General UI and behaviour settings.</summary>
    public GeneralSection General { get; set; } = new();

    /// <summary>Settings for RabbitMQ connection.</summary>
    public RabbitMqOptions RabbitMQ { get; set; } = new();

    /// <summary>Movement settings for the duck mascot.</summary>
    public DuckConfig Duck { get; set; } = new();

    /// <summary>Settings for Ollama chat service.</summary>
    public OllamaOptions Ollama { get; set; } = new();
}
