using DeskDuck.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DeskDuck.Helper
{
    /// <summary>
    /// Provides methods for resolving the application configuration file path and loading/saving settings.
    /// Settings are stored per-user in %LocalAppData%\DeskDuck so that they survive
    /// application updates without being overwritten.
    /// </summary>
    public class SettingsRepository : ISettingsRepository
    {
        public string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string duckFolder = Path.Combine(appData, "DeskDuck");

            if (!Directory.Exists(duckFolder))
            {
                Directory.CreateDirectory(duckFolder);
            }

            string userConfig = Path.Combine(duckFolder, "appsettings.json");

            if (!File.Exists(userConfig))
            {
                string baseConfig = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(baseConfig))
                {
                    File.Copy(baseConfig, userConfig);
                }
                else
                {
                    File.WriteAllText(userConfig, "{}");
                }
            }

            return userConfig;
        }

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
                Debug.WriteLine($"[SettingsRepository] Error loading config: {ex.Message}");
            }
            return new AppSettingsModel();
        }

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
                Debug.WriteLine($"[SettingsRepository] Error saving config: {ex.Message}");
            }
        }
    }
}
