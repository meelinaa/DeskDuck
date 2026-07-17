using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using DeskDuck.Models;

namespace DeskDuck.Helper
{
    /// <summary>
    /// Provides helper methods for resolving the application configuration file path and loading settings.
    /// Settings are stored per-user in %LocalAppData%\DeskDuck so that they survive
    /// application updates without being overwritten.
    /// </summary>
    public static class ConfigHelper
    {
        /// <summary>
        /// Returns the path to the user-specific appsettings.json file, creating the
        /// DeskDuck folder if it does not exist and seeding it from the bundled default
        /// config when the user file is absent. Falls back to an empty JSON object if
        /// no default config is bundled with the application.
        /// </summary>
        public static string GetConfigPath()
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

        /// <summary>
        /// Loads the user settings from the central appsettings.json file.
        /// NativeAOT-safe.
        /// </summary>
        public static AppSettingsModel LoadSettings()
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
                Debug.WriteLine($"[ConfigHelper] Error loading config: {ex.Message}");
            }
            return new AppSettingsModel();
        }
    }
}
