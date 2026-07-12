using DeskDuck.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Graphics;

namespace DeskDuck
{
    public sealed partial class SettingsWindow : Window
    {
        #region Win32 Interop
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetClassLong")]
        private static extern uint SetClassLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetClassLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
            }
            else
            {
                return new IntPtr(SetClassLong32(hWnd, nIndex, (uint)dwNewLong.ToInt32()));
            }
        }

        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int GCLP_HICONSM = -34;
        private const int GCL_HICON = -14;
        #endregion

        private readonly string _configPath;

        public SettingsWindow()
        {
            InitializeComponent();

            Title = "DeskDuck Einstellungen";
            AppWindow.Resize(new SizeInt32(420, 600));

            // Set TitleBar PreferredTheme to match application mode
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            // Make the settings window always on top of other windows
            OverlappedPresenter? presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }

            // Remove the icon in the top-left corner
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
            SetClassLong(hwnd, GCLP_HICONSM, IntPtr.Zero);
            SetClassLong(hwnd, GCL_HICON, IntPtr.Zero);

            _configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    AppSettingsModel? settings = JsonSerializer.Deserialize<AppSettingsModel>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (settings?.Publishers != null)
                    {
                        SystemMonitorOptions sys = settings.Publishers.SystemMonitor;
                        SysMonitorEnabled.IsOn = sys.Enabled;
                        SysCheckInterval.Value = sys.CheckIntervalSeconds;
                        BatteryEnabled.IsOn = sys.BatteryWarningEnabled;
                        BatteryThreshold.Value = sys.BatteryWarningThresholdPercent;
                        CpuEnabled.IsOn = sys.CpuWarningEnabled;
                        CpuThreshold.Value = sys.CpuWarningThresholdPercent;
                        RamEnabled.IsOn = sys.RamWarningEnabled;
                        RamThreshold.Value = sys.RamWarningThresholdPercent;

                        WeatherPublisherOptions weather = settings.Publishers.Weather;
                        WeatherEnabled.IsOn = weather.Enabled;
                        WeatherInterval.Value = weather.IntervalMinutes;
                        WeatherApiKey.Text = weather.ApiKey;
                        WeatherOverrideCity.Text = weather.OverrideCity;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsWindow] Error loading config: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppSettingsModel settings = new()
                {
                    Publishers = new PublishersSection
                    {
                        SystemMonitor = new SystemMonitorOptions
                        {
                            Enabled = SysMonitorEnabled.IsOn,
                            CheckIntervalSeconds = (int)SysCheckInterval.Value,
                            BatteryWarningEnabled = BatteryEnabled.IsOn,
                            BatteryWarningThresholdPercent = (int)BatteryThreshold.Value,
                            CpuWarningEnabled = CpuEnabled.IsOn,
                            CpuWarningThresholdPercent = (int)CpuThreshold.Value,
                            RamWarningEnabled = RamEnabled.IsOn,
                            RamWarningThresholdPercent = (int)RamThreshold.Value
                        },
                        Weather = new WeatherPublisherOptions
                        {
                            Enabled = WeatherEnabled.IsOn,
                            IntervalMinutes = (int)WeatherInterval.Value,
                            ApiKey = WeatherApiKey.Text,
                            Location = "auto",
                            OverrideCity = WeatherOverrideCity.Text
                        }
                    }
                };

                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_configPath, json);

                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsWindow] Error saving config: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
