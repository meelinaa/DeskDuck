using System;
using System.IO;

namespace DeskDuck.Helper
{
    public static class ConfigHelper
    {
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
                    // Fallback create empty object if nothing exists
                    File.WriteAllText(userConfig, "{}");
                }
            }
            
            return userConfig;
        }
    }
}
