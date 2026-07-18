using DeskDuck.Core.Models;

namespace DeskDuck.Core.Features.Settings;

/// <summary>
/// Abstraction for loading and saving application configuration.
/// Decouples view models and the host builder from direct file IO and static helpers.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Loads the complete application settings from the persistence store.
    /// </summary>
    AppSettingsModel LoadSettings();

    /// <summary>
    /// Saves the given application settings to the persistence store.
    /// </summary>
    void SaveSettings(AppSettingsModel settings);

    /// <summary>
    /// Returns the physical path to the configuration file.
    /// </summary>
    string GetConfigPath();
}
