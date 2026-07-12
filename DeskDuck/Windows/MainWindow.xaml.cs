using DeskDuck.Consumer;
using DeskDuck.Enums;
using DeskDuck.Helper;
using DeskDuck.Manager;
using DeskDuck.Models;
using DeskDuck.Publisher;
using DeskDuck.Services;
using DeskDuck.ViewModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DeskDuck
{
    /// <summary>
    /// Transparent overlay window that displays a walking duck on the desktop.
    /// The window is click-through so you can interact with apps underneath.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        #region Win32 Interop
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out PointStruct lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct PointStruct
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        private const int HOTKEY_ID = 1337;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_D = 0x44;
        private const uint WM_HOTKEY = 0x0312;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        #endregion

        private SUBCLASSPROC? _subclassProc;

        private readonly DuckMovementManager? _movementManager;
        private readonly RabbitMQBackgroundService? _rabbitMQService;
        private readonly IHost? _host;

        public MainViewModel MainViewModel { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            ConfigureOverlayWindow();
            LoadSettings();

            _movementManager = new DuckMovementManager(AppWindow, DispatcherQueue);
            _movementManager.StateChanged += OnDuckStateChanged;
            _movementManager.PositionChanged += OnDuckPositionChanged;
            _movementManager.Start();

            // Start RabbitMQ Background Service
            _rabbitMQService = new RabbitMQBackgroundService(
                DispatcherQueue,
                ShowNotification,
                HideNotification
            );
            _rabbitMQService.Start();

            // Start the Publisher services Host
            try
            {
                _host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        string configPath = ConfigHelper.GetConfigPath();
                        config.SetBasePath(Path.GetDirectoryName(configPath)!);
                        config.AddJsonFile(Path.GetFileName(configPath), optional: false, reloadOnChange: true);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        services.Configure<SystemMonitorOptions>(context.Configuration.GetSection("Publishers:SystemMonitor"));
                        services.Configure<WeatherPublisherOptions>(context.Configuration.GetSection("Publishers:Weather"));

                        services.AddSingleton<RabbitMqPublisher>();

                        services.AddHostedService<SystemMonitorPublisherService>();
                        services.AddHostedService<WeatherPublisherService>();
                    })
                    .Build();

                _host.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Failed to start: {ex.Message}");
            }

            Closed += OnWindowClosed;
        }

        private async void OnWindowClosed(object sender, WindowEventArgs args)
        {
            try
            {
                nint hwnd = WindowNative.GetWindowHandle(this);
                UnregisterHotKey(hwnd, HOTKEY_ID);
                if (_subclassProc != null)
                {
                    RemoveWindowSubclass(hwnd, _subclassProc, new IntPtr(1));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hotkey Cleanup] Error: {ex.Message}");
            }

            try
            {
                if (_host != null)
                {
                    // Use a cancellation token source with timeout to guarantee no hanging on close
                    using (CancellationTokenSource cts = new(TimeSpan.FromSeconds(5)))
                    {
                        await _host.StopAsync(cts.Token);
                    }
                    _host.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Error during shutdown: {ex.Message}");
            }

            try
            {
                if (_rabbitMQService != null)
                {
                    await _rabbitMQService.StopAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RabbitMQService] Error during shutdown: {ex.Message}");
            }
        }

        private void ShowNotification(NotificationMessage message)
        {
            MainViewModel.NotificationTitle = message.Title ?? string.Empty;
            MainViewModel.NotificationMessage = message.Message;

            string severity = message.Severity?.ToLowerInvariant() ?? string.Empty;
            string source = message.Source?.ToLowerInvariant() ?? string.Empty;

            if (severity == "warning")
            {
                MainViewModel.NotificationTextBrush = new SolidColorBrush(Color.FromArgb(255, 209, 52, 56));
            }
            else if (severity == "info" || source == "weather")
            {
                MainViewModel.NotificationTextBrush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
            }
            else
            {
                MainViewModel.NotificationTextBrush = new SolidColorBrush(Colors.Black);
            }

            MainViewModel.NotificationVisibility = Visibility.Visible;
        }

        private void HideNotification()
        {
            MainViewModel.NotificationVisibility = Visibility.Collapsed;
        }

        private bool _isDragging = false;
        private PointInt32 _dragStartWindowPos;
        private PointStruct _dragStartCursorPos;

        private ChatWindow? _chatWindow;
        private bool _isChatActive = false;
        private SettingsWindow? _settingsWindow;
        private bool _isSettingsActive = false;

        private bool _isContextMenuOpen = false;

        private void DuckContextMenu_Closed(object sender, object e)
        {
            _isContextMenuOpen = false;
            // Resume walking only if the chat window and settings window are not opened
            if (!_isChatActive && !_isSettingsActive)
            {
                _movementManager?.Resume();
            }
        }

        private void ChatWithAI_Click(object sender, RoutedEventArgs e)
        {
            _isChatActive = true;

            if (_chatWindow != null)
            {
                _chatWindow.Activate();
                return;
            }

            // Create new ChatWindow
            _chatWindow = new ChatWindow();
            AppWindow chatAppWindow = _chatWindow.AppWindow;

            chatAppWindow.Changed += ChatAppWindow_Changed;

            _chatWindow.Closed += (s, args) =>
            {
                chatAppWindow.Changed -= ChatAppWindow_Changed;
                _chatWindow = null;
                _isChatActive = false;
                // Resume movement after closing the chat window
                _movementManager?.Start();
            };

            // Freeze movement completely during chat
            _movementManager?.Stop();

            // Open chat window first so position/size are calculated
            _chatWindow.Activate();

            // Dock duck window next to top-left of chat window
            PointInt32 chatPos = chatAppWindow.Position;
            SizeInt32 chatSize = chatAppWindow.Size;

            int newX = chatPos.X - (AppWindow.Size.Width / 2);
            int newY = chatPos.Y - (AppWindow.Size.Height / 2);

            AppWindow.Move(new PointInt32(newX, newY));
            MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        private void ChatAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange && _chatWindow != null)
            {
                PointInt32 chatPos = sender.Position;

                int newX = chatPos.X - (AppWindow.Size.Width / 2);
                int newY = chatPos.Y - (AppWindow.Size.Height / 2);

                AppWindow.Move(new PointInt32(newX, newY));
                MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            _isSettingsActive = true;

            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            // Create new SettingsWindow
            _settingsWindow = new SettingsWindow();
            AppWindow settingsAppWindow = _settingsWindow.AppWindow;

            settingsAppWindow.Changed += SettingsAppWindow_Changed;

            _settingsWindow.Closed += (s, args) =>
            {
                settingsAppWindow.Changed -= SettingsAppWindow_Changed;
                _settingsWindow = null;
                _isSettingsActive = false;
                // Resume movement after closing the settings window
                _movementManager?.Start();
                LoadSettings();
            };

            // Freeze movement completely during settings
            _movementManager?.Stop();

            // Open settings window first so position/size are calculated
            _settingsWindow.Activate();

            // Dock duck window next to top-left of settings window
            PointInt32 settingsPos = settingsAppWindow.Position;
            SizeInt32 settingsSize = settingsAppWindow.Size;

            int newX = settingsPos.X - (AppWindow.Size.Width / 2);
            int newY = settingsPos.Y - (AppWindow.Size.Height / 2);

            AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        private void SettingsAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange && _settingsWindow != null)
            {
                PointInt32 settingsPos = sender.Position;

                int newX = settingsPos.X - (AppWindow.Size.Width / 2);
                int newY = settingsPos.Y - (AppWindow.Size.Height / 2);

                AppWindow.Move(new PointInt32(newX, newY));
                MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
            }
        }

        private void DuckImage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_movementManager == null || _isChatActive || _isSettingsActive) return;

            PointerPointProperties properties = e.GetCurrentPoint(sender as UIElement).Properties;

            // Linksklick -> Drag & Drop starten
            if (properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _movementManager.Pause();
                UpdateDuckVisual(DuckState.Held);

                GetCursorPos(out _dragStartCursorPos);
                _dragStartWindowPos = new PointInt32(AppWindow.Position.X, AppWindow.Position.Y);

                (sender as UIElement)?.CapturePointer(e.Pointer);
            }
            // Rechtsklick -> Kontextmenü anzeigen
            else if (properties.IsRightButtonPressed)
            {
                _movementManager.Pause();
                UpdateDuckVisual(DuckState.Waiting);

                // Show context menu
                FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
            }
        }

        private void DuckImage_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            GetCursorPos(out PointStruct currentCursorPos);
            int deltaX = currentCursorPos.X - _dragStartCursorPos.X;
            int deltaY = currentCursorPos.Y - _dragStartCursorPos.Y;

            int newX = _dragStartWindowPos.X + deltaX;
            int newY = _dragStartWindowPos.Y + deltaY;

            AppWindow.Move(new PointInt32(newX, newY));
            MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        private void DuckImage_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            (sender as UIElement)?.ReleasePointerCapture(e.Pointer);

            UpdateDuckVisual(DuckState.Waiting);

            if (!_isChatActive)
            {
                _movementManager?.Resume();
            }
        }

        private void OnDuckStateChanged(DuckState state)
        {
            UpdateDuckVisual(state);
        }

        private void OnDuckPositionChanged(int x, int y)
        {
            MainViewModel.CoordinatesText = $"X: {x}, Y: {y}";
        }

        private void UpdateDuckVisual(DuckState state)
        {
            string uriString = state switch
            {
                DuckState.WalkingLeft => "ms-appx:///Assets/Duck/duck-walking-to-left.gif",
                DuckState.WalkingRight => "ms-appx:///Assets/Duck/duck-walking-to-right.gif",
                DuckState.Held => "ms-appx:///Assets/Duck/pokeball.gif",
                _ => "ms-appx:///Assets/Duck/duck-sitting.gif"
            };

            MainViewModel.DuckImageUri = uriString;
        }

        private void ConfigureOverlayWindow()
        {
            // Transparenter Hintergrund via custom SystemBackdrop
            SystemBackdrop = new TransparentBackdrop();

            AppWindow appWindow = this.AppWindow;
            nint hwnd = WindowNative.GetWindowHandle(this);

            // Rahmenlos und immer im Vordergrund
            OverlappedPresenter presenter = OverlappedPresenter.CreateForToolWindow();
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
            appWindow.SetPresenter(presenter);

            // Fenstergröße auf kompakte Ente-Größe setzen
            appWindow.Resize(new SizeInt32(300, 300));

            // Klickdurchlässig machen NUR für transparente Bereiche.
            // Ohne WS_EX_TRANSPARENT können sichtbare UI-Elemente wie die Ente Mausklicks empfangen.
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            _ = SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            // Register global hotkey and subclass window proc
            try
            {
                _subclassProc = new SUBCLASSPROC(NewWindowProc);
                SetWindowSubclass(hwnd, _subclassProc, new IntPtr(1), IntPtr.Zero);
                RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_SHIFT, VK_D);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hotkey Registration] Failed: {ex.Message}");
            }
        }

        private IntPtr NewWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (!_isDragging && !_isChatActive && !_isSettingsActive && _movementManager != null)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        DisplayArea displayArea = DisplayArea.Primary;
                        RectInt32 workArea = displayArea.WorkArea;

                        double centerX = workArea.X + (workArea.Width - AppWindow.Size.Width) / 2.0;
                        double centerY = workArea.Y + (workArea.Height - AppWindow.Size.Height) / 2.0;

                        _movementManager.TeleportTo(centerX, centerY);
                        MainViewModel.CoordinatesText = $"X: {(int)centerX}, Y: {(int)centerY}";
                    });
                }
                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void LoadSettings()
        {
            try
            {
                string configPath = ConfigHelper.GetConfigPath();
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    AppSettingsModel? settings = JsonSerializer.Deserialize<AppSettingsModel>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        TypeInfoResolver = AppJsonSerializerContext.Default
                    });

                    if (settings?.General != null)
                    {
                        MainViewModel.CoordinatesVisibility = settings.General.ShowCoordinates ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        MainViewModel.CoordinatesVisibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error loading config: {ex.Message}");
            }
        }
    }
}
