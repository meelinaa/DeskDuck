using DeskDuck.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DeskDuck.Core.Features.Settings;

/// <summary>
/// Provides methods for resolving the application configuration file path and loading/saving settings.
/// Settings are stored per-user in %LocalAppData%\DeskDuck so that they survive
/// application updates without being overwritten.
/// </summary>
public class SettingsRepository(ILogger<SettingsRepository> logger) : ISettingsRepository
{
    private readonly ILogger<SettingsRepository> _logger = logger;

    /// <inheritdoc/>
    public string GetConfigPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string duckFolder = Path.Combine(appData, "DeskDuck");

        if (!Directory.Exists(duckFolder))
            Directory.CreateDirectory(duckFolder);
        
        string userConfig = Path.Combine(duckFolder, "appsettings.json");

        if (!File.Exists(userConfig))
        {
            string baseConfig = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(baseConfig))
                File.Copy(baseConfig, userConfig);
            
            else            
                File.WriteAllText(userConfig, "{}");
        }

        return userConfig;
    }

    /// <inheritdoc/>
    public AppSettingsModel LoadSettings()
    {
        try
        {
            string configPath = GetConfigPath();
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<AppSettingsModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    TypeInfoResolver = AppJsonSerializerContext.Default
                }) ?? new AppSettingsModel();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading config");
        }
        return new AppSettingsModel();
    }

    /// <inheritdoc/>
    public void SaveSettings(AppSettingsModel settings)
    {
        try
        {
            string configPath = GetConfigPath();
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                TypeInfoResolver = AppJsonSerializerContext.Default
            };
            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving config");
        }
    }
}
