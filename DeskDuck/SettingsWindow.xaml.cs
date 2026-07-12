using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

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
            this.InitializeComponent();

            this.Title = "DeskDuck Einstellungen";
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(420, 600));

            // Set TitleBar PreferredTheme to match application mode
            this.AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            // Make the settings window always on top of other windows
            var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }

            // Remove the icon in the top-left corner
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
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
                    var settings = JsonSerializer.Deserialize<AppSettingsModel>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (settings?.Publishers != null)
                    {
                        var sys = settings.Publishers.SystemMonitor;
                        SysMonitorEnabled.IsOn = sys.Enabled;
                        SysCheckInterval.Value = sys.CheckIntervalSeconds;
                        BatteryEnabled.IsOn = sys.BatteryWarningEnabled;
                        BatteryThreshold.Value = sys.BatteryWarningThresholdPercent;
                        CpuEnabled.IsOn = sys.CpuWarningEnabled;
                        CpuThreshold.Value = sys.CpuWarningThresholdPercent;
                        RamEnabled.IsOn = sys.RamWarningEnabled;
                        RamThreshold.Value = sys.RamWarningThresholdPercent;

                        var weather = settings.Publishers.Weather;
                        WeatherEnabled.IsOn = weather.Enabled;
                        WeatherInterval.Value = weather.IntervalMinutes;
                        WeatherApiKey.Text = weather.ApiKey;
                        WeatherOverrideCity.Text = weather.OverrideCity;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Error loading config: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new AppSettingsModel
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

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_configPath, json);

                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Error saving config: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class AppSettingsModel
    {
        public PublishersSection Publishers { get; set; } = new();
    }

    public class PublishersSection
    {
        public SystemMonitorOptions SystemMonitor { get; set; } = new();
        public WeatherPublisherOptions Weather { get; set; } = new();
    }
}
