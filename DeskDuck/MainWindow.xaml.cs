using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading;
using System.Runtime.InteropServices;
using WinRT.Interop;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

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

        private DuckMovementManager? _movementManager;
        private RabbitMQBackgroundService? _rabbitMQService;
        private IHost? _host;

        public MainViewModel ViewModel { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            ConfigureOverlayWindow();

            _movementManager = new DuckMovementManager(this.AppWindow, this.DispatcherQueue);
            _movementManager.StateChanged += OnDuckStateChanged;
            _movementManager.PositionChanged += OnDuckPositionChanged;
            _movementManager.Start();

            // Start RabbitMQ Background Service
            _rabbitMQService = new RabbitMQBackgroundService(
                this.DispatcherQueue,
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
                        config.SetBasePath(AppContext.BaseDirectory);
                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
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
                System.Diagnostics.Debug.WriteLine($"[Host] Failed to start: {ex.Message}");
            }

            this.Closed += OnWindowClosed;
        }

        private async void OnWindowClosed(object sender, WindowEventArgs args)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                UnregisterHotKey(hwnd, HOTKEY_ID);
                if (_subclassProc != null)
                {
                    RemoveWindowSubclass(hwnd, _subclassProc, new IntPtr(1));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Hotkey Cleanup] Error: {ex.Message}");
            }

            try
            {
                if (_host != null)
                {
                    // Use a cancellation token source with timeout to guarantee no hanging on close
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        await _host.StopAsync(cts.Token);
                    }
                    _host.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Host] Error during shutdown: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[RabbitMQService] Error during shutdown: {ex.Message}");
            }
        }

        private void ShowNotification(NotificationMessage message)
        {
            ViewModel.NotificationTitle = message.Title ?? string.Empty;
            ViewModel.NotificationMessage = message.Message;

            var severity = message.Severity?.ToLowerInvariant() ?? "";
            var source = message.Source?.ToLowerInvariant() ?? "";

            if (severity == "warning")
            {
                ViewModel.NotificationTextBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 209, 52, 56));
            }
            else if (severity == "info" || source == "weather")
            {
                ViewModel.NotificationTextBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
            }
            else
            {
                ViewModel.NotificationTextBrush = new SolidColorBrush(Microsoft.UI.Colors.Black);
            }

            ViewModel.NotificationVisibility = Visibility.Visible;
        }

        private void HideNotification()
        {
            ViewModel.NotificationVisibility = Visibility.Collapsed;
        }

        private bool _isDragging = false;
        private Windows.Graphics.PointInt32 _dragStartWindowPos;
        private PointStruct _dragStartCursorPos;

        private ChatWindow? _chatWindow;
        private bool _isChatActive = false;
        private SettingsWindow? _settingsWindow;
        private bool _isSettingsActive = false;

        private void DuckContextMenu_Closed(object sender, object e)
        {
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
            var chatAppWindow = _chatWindow.AppWindow;
            
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
            var chatPos = chatAppWindow.Position;
            var chatSize = chatAppWindow.Size;

            int newX = chatPos.X - (this.AppWindow.Size.Width / 2);
            int newY = chatPos.Y - (this.AppWindow.Size.Height / 2);

            this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            ViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        private void ChatAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange && _chatWindow != null)
            {
                var chatPos = sender.Position;
                var chatSize = sender.Size;

                int newX = chatPos.X - (this.AppWindow.Size.Width / 2);
                int newY = chatPos.Y - (this.AppWindow.Size.Height / 2);

                this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
                ViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
            }
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
            var settingsAppWindow = _settingsWindow.AppWindow;
            
            settingsAppWindow.Changed += SettingsAppWindow_Changed;

            _settingsWindow.Closed += (s, args) =>
            {
                settingsAppWindow.Changed -= SettingsAppWindow_Changed;
                _settingsWindow = null;
                _isSettingsActive = false;
                // Resume movement after closing the settings window
                _movementManager?.Start();
            };

            // Freeze movement completely during settings
            _movementManager?.Stop();

            // Open settings window first so position/size are calculated
            _settingsWindow.Activate();

            // Dock duck window next to top-left of settings window
            var settingsPos = settingsAppWindow.Position;
            var settingsSize = settingsAppWindow.Size;

            int newX = settingsPos.X - (this.AppWindow.Size.Width / 2);
            int newY = settingsPos.Y - (this.AppWindow.Size.Height / 2);

            this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            ViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        private void SettingsAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange && _settingsWindow != null)
            {
                var settingsPos = sender.Position;
                var settingsSize = sender.Size;

                int newX = settingsPos.X - (this.AppWindow.Size.Width / 2);
                int newY = settingsPos.Y - (this.AppWindow.Size.Height / 2);

                this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
                ViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
            }
        }

        private void DuckImage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_movementManager == null || _isChatActive || _isSettingsActive) return;

            var properties = e.GetCurrentPoint(sender as UIElement).Properties;

            // Linksklick -> Drag & Drop starten
            if (properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _movementManager.Pause();
                UpdateDuckVisual(DuckState.Held);

                GetCursorPos(out _dragStartCursorPos);
                _dragStartWindowPos = new Windows.Graphics.PointInt32(this.AppWindow.Position.X, this.AppWindow.Position.Y);

                (sender as UIElement)?.CapturePointer(e.Pointer);
            }
            // Rechtsklick -> Kontextmenü anzeigen
            else if (properties.IsRightButtonPressed)
            {
                _movementManager.Pause();
                UpdateDuckVisual(DuckState.Waiting);

                // Show context menu
                Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
            }
        }

        private void DuckImage_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            GetCursorPos(out var currentCursorPos);
            var deltaX = currentCursorPos.X - _dragStartCursorPos.X;
            var deltaY = currentCursorPos.Y - _dragStartCursorPos.Y;

            var newX = _dragStartWindowPos.X + deltaX;
            var newY = _dragStartWindowPos.Y + deltaY;

            this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            ViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        private void DuckImage_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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
            ViewModel.CoordinatesText = $"X: {x}, Y: {y}";
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

            ViewModel.DuckImageUri = uriString;
        }

        private void ConfigureOverlayWindow()
        {
            // Transparenter Hintergrund via custom SystemBackdrop
            SystemBackdrop = new TransparentBackdrop();

            var appWindow = this.AppWindow;
            var hwnd = WindowNative.GetWindowHandle(this);

            // Rahmenlos und immer im Vordergrund
            var presenter = OverlappedPresenter.CreateForToolWindow();
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
            appWindow.SetPresenter(presenter);

            // Fenstergröße auf kompakte Ente-Größe setzen
            appWindow.Resize(new Windows.Graphics.SizeInt32(300, 300));

            // Klickdurchlässig machen NUR für transparente Bereiche.
            // Ohne WS_EX_TRANSPARENT können sichtbare UI-Elemente wie die Ente Mausklicks empfangen.
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            // Register global hotkey and subclass window proc
            try
            {
                _subclassProc = new SUBCLASSPROC(NewWindowProc);
                SetWindowSubclass(hwnd, _subclassProc, new IntPtr(1), IntPtr.Zero);
                RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_SHIFT, VK_D);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Hotkey Registration] Failed: {ex.Message}");
            }
        }

        private IntPtr NewWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (!_isDragging && !_isChatActive && !_isSettingsActive && _movementManager != null)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
                        var workArea = displayArea.WorkArea;

                        double centerX = workArea.X + (workArea.Width - this.AppWindow.Size.Width) / 2.0;
                        double centerY = workArea.Y + (workArea.Height - this.AppWindow.Size.Height) / 2.0;

                        _movementManager.TeleportTo(centerX, centerY);
                        ViewModel.CoordinatesText = $"X: {(int)centerX}, Y: {(int)centerY}";
                    });
                }
                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }

    /// <summary>
    /// Custom SystemBackdrop that renders a fully transparent background,
    /// allowing the desktop and other windows to show through.
    /// </summary>
    public class TransparentBackdrop : SystemBackdrop
    {
        protected override void OnTargetConnected(
            ICompositionSupportsSystemBackdrop connectedTarget,
            XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            // In WinUI 3, ICompositionSupportsSystemBackdrop is a WinRT interface.
            // Under the hood, we can retrieve the Windows.UI.Composition.Compositor.
            if (connectedTarget is Windows.UI.Composition.CompositionObject compositionObject)
            {
                var compositor = compositionObject.Compositor;
                var transparentBrush = compositor.CreateColorBrush(
                    new Windows.UI.Color { A = 0, R = 0, G = 0, B = 0 });
                connectedTarget.SystemBackdrop = transparentBrush;
            }
        }
    }
}
