using DeskDuck.Enums;
using DeskDuck.Helper;
using DeskDuck.Manager;
using DeskDuck.Models;
using DeskDuck.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using DeskDuck.Features.Chat;
using DeskDuck.Features.Settings;
using DeskDuck.Features.Messaging;

namespace DeskDuck.Features.Shell
{
    /// <summary>
    /// Transparent overlay window that displays a walking duck on the desktop.
    /// The window is click-through so you can interact with apps underneath.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private Win32WindowHelper.SUBCLASSPROC? _subclassProc;

        private readonly DuckMovementManager _movementManager;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IServiceProvider _serviceProvider;

        public MainViewModel MainViewModel { get; }

        /// <summary>
        /// Initializes the main window, configures the overlay, loads settings,
        /// starts duck movement, and launches all background services.
        /// </summary>
        public MainWindow(
            IServiceProvider serviceProvider,
            ISettingsRepository settingsRepository,
            IOptions<DuckConfig> duckConfig,
            IOptionsMonitor<GeneralSection> generalConfig)
        {
            _serviceProvider = serviceProvider;
            _settingsRepository = settingsRepository;

            InitializeComponent();
            
            MainViewModel = new MainViewModel(DispatcherQueue);
            
            ConfigureOverlayWindow();

            MainViewModel.CoordinatesVisibility = generalConfig.CurrentValue.ShowCoordinates ? Visibility.Visible : Visibility.Collapsed;
            generalConfig.OnChange(config =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    MainViewModel.CoordinatesVisibility = config.ShowCoordinates ? Visibility.Visible : Visibility.Collapsed;
                });
            });

            _movementManager = new DuckMovementManager(AppWindow, DispatcherQueue, duckConfig);
            _movementManager.StateChanged += OnDuckStateChanged;
            _movementManager.PositionChanged += OnDuckPositionChanged;
            _movementManager.Start();

            Closed += OnWindowClosed;
        }

        /// <summary>
        /// Cleans up resources when the window closes: unregisters the global hotkey,
        /// removes the window subclass, gracefully stops the publisher host with a 5-second
        /// timeout to prevent hanging, and shuts down the RabbitMQ background service.
        /// </summary>
        private async void OnWindowClosed(object sender, WindowEventArgs args)
        {
            try
            {
                nint hwnd = WindowNative.GetWindowHandle(this);
                Win32WindowHelper.UnregisterHotkey(hwnd, Win32WindowHelper.HOTKEY_ID);
                if (_subclassProc != null)
                {
                    Win32WindowHelper.RemoveSubclass(hwnd, _subclassProc, new IntPtr(1));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hotkey Cleanup] Error: {ex.Message}");
            }

            try
            {
                if (_movementManager != null)
                {
                    _movementManager.StateChanged -= OnDuckStateChanged;
                    _movementManager.PositionChanged -= OnDuckPositionChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Movement Cleanup] Error: {ex.Message}");
            }



        }

        private bool _isDragging = false;
        private PointInt32 _dragStartWindowPos;
        private Win32WindowHelper.PointStruct _dragStartCursorPos;

        private ChatWindow? _chatWindow;
        private bool _isChatActive = false;
        private SettingsWindow? _settingsWindow;
        private bool _isSettingsActive = false;

        private bool _isContextMenuOpen = false;

        /// <summary>
        /// Resumes duck movement when the context menu closes, provided neither the
        /// chat nor the settings window is currently open.
        /// </summary>
        private void DuckContextMenu_Closed(object sender, object e)
        {
            _isContextMenuOpen = false;
            if (!_isChatActive && !_isSettingsActive)
            {
                _movementManager?.Resume();
            }
        }

        /// <summary>
        /// Opens the chat window and freezes duck movement for the duration of the chat session.
        /// If a chat window already exists, it is simply brought to the foreground.
        /// The duck is repositioned to sit next to the chat window so it remains visible.
        /// </summary>
        private void ChatWithAI_Click(object sender, RoutedEventArgs e)
        {
            _isChatActive = true;

            if (_chatWindow != null)
            {
                _chatWindow.Activate();
                return;
            }

            var chatViewModel = _serviceProvider.GetRequiredService<ChatViewModel>();
            _chatWindow = new ChatWindow(chatViewModel);
            AppWindow chatAppWindow = _chatWindow.AppWindow;

            chatAppWindow.Changed += ChatAppWindow_Changed;

            _chatWindow.Closed += (s, args) =>
            {
                chatAppWindow.Changed -= ChatAppWindow_Changed;
                _chatWindow = null;
                _isChatActive = false;
                _movementManager?.Start();
            };

            _movementManager?.Stop();

            _chatWindow.Activate();

            PointInt32 chatPos = chatAppWindow.Position;
            SizeInt32 chatSize = chatAppWindow.Size;

            int newX = chatPos.X - (AppWindow.Size.Width / 2);
            int newY = chatPos.Y - (AppWindow.Size.Height / 2);

            AppWindow.Move(new PointInt32(newX, newY));
            MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        /// <summary>
        /// Keeps the duck window anchored relative to the chat window whenever the
        /// chat window is moved by the user.
        /// </summary>
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

        /// <summary>
        /// Exits the application immediately when the user selects "Exit" from the context menu.
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        /// <summary>
        /// Opens the settings window and freezes duck movement for the duration of the session.
        /// If the settings window already exists, it is brought to the foreground.
        /// The duck is repositioned next to the settings window and settings are reloaded on close.
        /// </summary>
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            _isSettingsActive = true;

            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            var settingsViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
            _settingsWindow = new SettingsWindow(settingsViewModel);
            AppWindow settingsAppWindow = _settingsWindow.AppWindow;

            settingsAppWindow.Changed += SettingsAppWindow_Changed;

            _settingsWindow.Closed += (s, args) =>
            {
                settingsAppWindow.Changed -= SettingsAppWindow_Changed;
                _settingsWindow = null;
                _isSettingsActive = false;
                _movementManager?.Start();
            };

            _movementManager?.Stop();

            _settingsWindow.Activate();

            PointInt32 settingsPos = settingsAppWindow.Position;
            SizeInt32 settingsSize = settingsAppWindow.Size;

            int newX = settingsPos.X - (AppWindow.Size.Width / 2);
            int newY = settingsPos.Y - (AppWindow.Size.Height / 2);

            AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        /// <summary>
        /// Keeps the duck window anchored relative to the settings window whenever the
        /// settings window is moved by the user.
        /// </summary>
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

        /// <summary>
        /// Handles pointer press events on the duck image.
        /// Left-click starts a drag operation and captures the pointer so movement
        /// is tracked even when the cursor leaves the element.
        /// Right-click pauses movement and shows the context menu flyout.
        /// </summary>
        private void DuckImage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_movementManager == null || _isChatActive || _isSettingsActive) return;

            PointerPointProperties properties = e.GetCurrentPoint(sender as UIElement).Properties;

            if (properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _movementManager.Pause();
                UpdateDuckVisual(DuckState.Held);

                Win32WindowHelper.GetCursorPosition(out _dragStartCursorPos);
                _dragStartWindowPos = new PointInt32(AppWindow.Position.X, AppWindow.Position.Y);

                (sender as UIElement)?.CapturePointer(e.Pointer);
            }
            else if (properties.IsRightButtonPressed)
            {
                _isContextMenuOpen = true;
                _movementManager.Pause();
                UpdateDuckVisual(DuckState.Waiting);

                FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
            }
        }

        /// <summary>
        /// Moves the duck window by the delta between the current cursor position and the
        /// position recorded when dragging started, producing smooth drag-and-drop behaviour.
        /// </summary>
        private void DuckImage_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            Win32WindowHelper.GetCursorPosition(out Win32WindowHelper.PointStruct currentCursorPos);
            int deltaX = currentCursorPos.X - _dragStartCursorPos.X;
            int deltaY = currentCursorPos.Y - _dragStartCursorPos.Y;

            int newX = _dragStartWindowPos.X + deltaX;
            int newY = _dragStartWindowPos.Y + deltaY;

            AppWindow.Move(new PointInt32(newX, newY));
            MainViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        /// <summary>
        /// Ends the drag operation, releases the pointer capture, and resumes duck
        /// movement unless the chat window is currently open.
        /// </summary>
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

        /// <summary>
        /// Forwards duck state changes from the movement manager to the visual layer.
        /// </summary>
        private void OnDuckStateChanged(DuckState state)
        {
            UpdateDuckVisual(state);
        }

        /// <summary>
        /// Updates the coordinates label in the view model whenever the duck moves.
        /// </summary>
        private void OnDuckPositionChanged(int x, int y)
        {
            MainViewModel.CoordinatesText = $"X: {x}, Y: {y}";
        }

        /// <summary>
        /// Swaps the duck image URI to match the current movement state so the correct
        /// animation (walking left/right, held, or sitting) is displayed.
        /// </summary>
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

        /// <summary>
        /// Configures the window as a transparent, always-on-top, borderless overlay.
        /// WS_EX_LAYERED and WS_EX_TOOLWINDOW ensure the window is invisible to the taskbar
        /// while still allowing visible UI elements (the duck) to receive mouse input.
        /// WS_EX_NOACTIVATE prevents the overlay from stealing focus from other applications.
        /// A global hotkey (Ctrl+Alt+Shift+D) is registered via a window subclass so the
        /// duck can be teleported back to the screen center at any time.
        /// </summary>
        private void ConfigureOverlayWindow()
        {
            SystemBackdrop = new TransparentBackdrop();

            AppWindow appWindow = this.AppWindow;
            nint hwnd = WindowNative.GetWindowHandle(this);

            OverlappedPresenter presenter = OverlappedPresenter.CreateForToolWindow();
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
            appWindow.SetPresenter(presenter);

            appWindow.Resize(new SizeInt32(300, 300));

            Win32WindowHelper.ConfigureOverlayStyles(hwnd);

            try
            {
                _subclassProc = new Win32WindowHelper.SUBCLASSPROC(NewWindowProc);
                Win32WindowHelper.RegisterSubclass(hwnd, _subclassProc, new IntPtr(1), IntPtr.Zero);
                Win32WindowHelper.RegisterHotkey(hwnd, Win32WindowHelper.HOTKEY_ID, Win32WindowHelper.MOD_CONTROL | Win32WindowHelper.MOD_ALT | Win32WindowHelper.MOD_SHIFT, Win32WindowHelper.VK_D);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hotkey Registration] Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Custom window procedure that intercepts WM_HOTKEY messages.
        /// When the registered hotkey fires and no modal interaction is active,
        /// the duck is teleported to the center of the primary display's work area.
        /// All other messages are forwarded to the default subclass procedure.
        /// </summary>
        private IntPtr NewWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == Win32WindowHelper.WM_HOTKEY && wParam.ToInt32() == Win32WindowHelper.HOTKEY_ID)
            {
                if (!_isDragging && !_isChatActive && !_isSettingsActive && !_isContextMenuOpen && _movementManager != null)
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

            return Win32WindowHelper.DefaultSubclassProc(hWnd, uMsg, wParam, lParam);
        }

    }
}
