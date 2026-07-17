namespace DeskDuck.Models;

/// <summary>
/// General UI settings that apply to the main overlay window.
/// Maps to the "General" section of appsettings.json.
/// </summary>
public class GeneralSection
{
    /// <summary>
    /// When <c>true</c>, the duck's current screen coordinates are displayed beneath it.
    /// Useful for debugging window placement; can be hidden in production via the settings UI.
    /// </summary>
    public bool ShowCoordinates { get; set; } = true;
}
