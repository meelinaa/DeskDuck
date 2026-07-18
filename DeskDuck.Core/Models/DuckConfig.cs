namespace DeskDuck.Core.Models;

/// <summary>
/// Movement configuration for the duck overlay, loaded from config.json at startup.
/// Controls how fast the duck walks and how long it waits between destinations.
/// </summary>
public class DuckConfig
{
    /// <summary>Pixels per timer tick (~60 FPS) the duck moves toward its target.</summary>
    public double Speed { get; set; } = 2.0;

    /// <summary>Minimum number of seconds the duck waits at a destination before moving again.</summary>
    public int MinWaitSeconds { get; set; } = 5;

    /// <summary>Maximum number of seconds the duck waits at a destination before moving again.</summary>
    public int MaxWaitSeconds { get; set; } = 15;
}
