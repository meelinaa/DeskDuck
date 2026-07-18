using CommunityToolkit.Mvvm.ComponentModel;
using DeskDuck.Core.Features.SystemMonitor;
using DeskDuck.Core.Features.Weather;
using DeskDuck.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeskDuck.Core.Features.Settings;

/// <summary>
/// View model for the settings window. Exposes bindable properties for all settings and
/// delegates persistence to ISettingsRepository.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(ISettingsRepository settingsRepository, ILogger<SettingsViewModel> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
        Load();
    }

    public string ConfigPath => _settingsRepository.GetConfigPath();
    [ObservableProperty]
    public partial bool ShowCoordinatesEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool SysMonitorEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double SysCheckInterval { get; set; } = 10;

    [ObservableProperty]
    public partial bool BatteryEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double BatteryThreshold { get; set; } = 20;

    [ObservableProperty]
    public partial bool CpuEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double CpuThreshold { get; set; } = 85;

    [ObservableProperty]
    public partial bool RamEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double RamThreshold { get; set; } = 85;

    [ObservableProperty]
    public partial bool WeatherEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double WeatherInterval { get; set; } = 30;

    [ObservableProperty]
    public partial string WeatherApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WeatherOverrideCity { get; set; } = string.Empty;

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
            _logger.LogError(ex, "Error loading config");
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
