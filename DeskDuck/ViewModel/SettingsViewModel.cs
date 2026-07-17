using DeskDuck.Helper;
using DeskDuck.Models;
using System;
using System.Diagnostics;

namespace DeskDuck.ViewModel
{
    /// <summary>
    /// View model for the settings window. Exposes bindable properties for all settings and
    /// delegates persistence to ISettingsRepository.
    /// </summary>
    public partial class SettingsViewModel(ISettingsRepository settingsRepository) : ViewModelBase
    {
        private readonly ISettingsRepository _settingsRepository = settingsRepository;

        public string ConfigPath => _settingsRepository.GetConfigPath();
        private bool _showCoordinatesEnabled = true;
        private bool _sysMonitorEnabled = true;
        private double _sysCheckInterval = 10;
        private bool _batteryEnabled = true;
        private double _batteryThreshold = 20;
        private bool _cpuEnabled = true;
        private double _cpuThreshold = 85;
        private bool _ramEnabled = true;
        private double _ramThreshold = 85;
        private bool _weatherEnabled = true;
        private double _weatherInterval = 30;
        private string _weatherApiKey = string.Empty;
        private string _weatherOverrideCity = string.Empty;

        /// <summary>Gets or sets whether coordinates are displayed on the main overlay.</summary>
        public bool ShowCoordinatesEnabled
        {
            get => _showCoordinatesEnabled;
            set => SetProperty(ref _showCoordinatesEnabled, value);
        }

        /// <summary>Gets or sets whether the system health monitor is enabled.</summary>
        public bool SysMonitorEnabled
        {
            get => _sysMonitorEnabled;
            set => SetProperty(ref _sysMonitorEnabled, value);
        }

        /// <summary>Gets or sets the system metrics checking interval in seconds.</summary>
        public double SysCheckInterval
        {
            get => _sysCheckInterval;
            set => SetProperty(ref _sysCheckInterval, value);
        }

        /// <summary>Gets or sets whether battery low alerts are enabled.</summary>
        public bool BatteryEnabled
        {
            get => _batteryEnabled;
            set => SetProperty(ref _batteryEnabled, value);
        }

        /// <summary>Gets or sets the battery low warning threshold percentage.</summary>
        public double BatteryThreshold
        {
            get => _batteryThreshold;
            set => SetProperty(ref _batteryThreshold, value);
        }

        /// <summary>Gets or sets whether high CPU usage warnings are enabled.</summary>
        public bool CpuEnabled
        {
            get => _cpuEnabled;
            set => SetProperty(ref _cpuEnabled, value);
        }

        /// <summary>Gets or sets the CPU warning threshold percentage.</summary>
        public double CpuThreshold
        {
            get => _cpuThreshold;
            set => SetProperty(ref _cpuThreshold, value);
        }

        /// <summary>Gets or sets whether high RAM usage warnings are enabled.</summary>
        public bool RamEnabled
        {
            get => _ramEnabled;
            set => SetProperty(ref _ramEnabled, value);
        }

        /// <summary>Gets or sets the RAM warning threshold percentage.</summary>
        public double RamThreshold
        {
            get => _ramThreshold;
            set => SetProperty(ref _ramThreshold, value);
        }

        /// <summary>Gets or sets whether the weather publisher is enabled.</summary>
        public bool WeatherEnabled
        {
            get => _weatherEnabled;
            set => SetProperty(ref _weatherEnabled, value);
        }

        /// <summary>Gets or sets the weather update interval in minutes.</summary>
        public double WeatherInterval
        {
            get => _weatherInterval;
            set => SetProperty(ref _weatherInterval, value);
        }

        /// <summary>Gets or sets the OpenWeatherMap API Key.</summary>
        public string WeatherApiKey
        {
            get => _weatherApiKey;
            set => SetProperty(ref _weatherApiKey, value ?? string.Empty);
        }

        /// <summary>Gets or sets the override city for weather reports.</summary>
        public string WeatherOverrideCity
        {
            get => _weatherOverrideCity;
            set => SetProperty(ref _weatherOverrideCity, value ?? string.Empty);
        }

        /// <summary>
        /// Reads settings from the central appsettings.json and populates view model properties.
        /// </summary>
        public void Load()
        {
            try
            {
                AppSettingsModel settings = _settingsRepository.LoadSettings();

                ShowCoordinatesEnabled = settings.General.ShowCoordinates;

                if (settings.Publishers != null)
                {
                    SystemMonitorOptions sys = settings.Publishers.SystemMonitor;
                    SysMonitorEnabled = sys.Enabled;
                    SysCheckInterval = sys.CheckIntervalSeconds;
                    BatteryEnabled = sys.BatteryWarningEnabled;
                    BatteryThreshold = sys.BatteryWarningThresholdPercent;
                    CpuEnabled = sys.CpuWarningEnabled;
                    CpuThreshold = sys.CpuWarningThresholdPercent;
                    RamEnabled = sys.RamWarningEnabled;
                    RamThreshold = sys.RamWarningThresholdPercent;

                    WeatherPublisherOptions weather = settings.Publishers.Weather;
                    WeatherEnabled = weather.Enabled;
                    WeatherInterval = weather.IntervalMinutes;
                    WeatherApiKey = weather.ApiKey;
                    WeatherOverrideCity = weather.OverrideCity;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Error loading config: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves current property values back to the central appsettings.json file,
        /// while preserving all other configuration sections (e.g. RabbitMQ, Duck, Ollama).
        /// </summary>
        public void Save()
        {
            AppSettingsModel settings = _settingsRepository.LoadSettings();

            settings.General = new GeneralSection
            {
                ShowCoordinates = ShowCoordinatesEnabled
            };

            settings.Publishers = new PublishersSection
            {
                SystemMonitor = new SystemMonitorOptions
                {
                    Enabled = SysMonitorEnabled,
                    CheckIntervalSeconds = (int)SysCheckInterval,
                    BatteryWarningEnabled = BatteryEnabled,
                    BatteryWarningThresholdPercent = (int)BatteryThreshold,
                    CpuWarningEnabled = CpuEnabled,
                    CpuWarningThresholdPercent = (int)CpuThreshold,
                    RamWarningEnabled = RamEnabled,
                    RamWarningThresholdPercent = (int)RamThreshold
                },
                Weather = new WeatherPublisherOptions
                {
                    Enabled = WeatherEnabled,
                    IntervalMinutes = (int)WeatherInterval,
                    ApiKey = WeatherApiKey,
                    OverrideCity = WeatherOverrideCity
                }
            };

            _settingsRepository.SaveSettings(settings);
        }
    }
}
