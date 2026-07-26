using DeskDuck.Core.Features.Movement;
using DeskDuck.Core.Features.Settings;
using DeskDuck.Core.Features.Shell;
using DeskDuck.Core.Helper;
using DeskDuck.Core.Models;
using DeskDuck.Helper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace DeskDuck.Features.Shell;

/// <summary>
/// Transparent overlay window that displays a walking duck on the desktop.
/// The window is click-through so you can interact with apps underneath.
/// All WinUI-specific concerns (Brush creation, Visibility mapping, DispatcherQueue
/// marshalling) are handled here rather than in the framework-agnostic ViewModel.
/// </summary>
public sealed partial class MainWindow : Window
{
    private Win32WindowHelper.SUBCLASSPROC? _subclassProc;

    private readonly IDuckMovementController _movementManager;
    private readonly ILogger<MainWindow> _logger;
    private readonly IWindowService _windowService;

    /// <summary>
    /// Gets the view model bound to this window.
    /// </summary>
    public MainViewModel MainViewModel { get; }

    /// <summary>
    /// Converts a bool to Visibility. Used by x:Bind in the XAML.
    /// </summary>
    public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="movementManager">The manager controlling the duck's movement logic.</param>
    /// <param name="windowManager">The manager for opening auxiliary windows.</param>
    /// <param name="generalConfig">The configuration options for general settings.</param>
    /// <param name="logger">The logger for this window.</param>
    /// <param name="windowService">The window service for Win32 API interactions.</param>
    /// <param name="messenger">The messenger for sending and receiving commands.</param>
    public MainWindow(
        IDuckMovementController movementManager,
        IDuckWindowManager windowManager,
        IOptionsMonitor<GeneralSection> generalConfig,
        ILogger<MainWindow> logger,
        IWindowService windowService,
        CommunityToolkit.Mvvm.Messaging.IMessenger messenger)
    {
        _movementManager = movementManager;
        _logger = logger;
        _windowService = windowService;

        InitializeComponent();

        MainViewModel = new MainViewModel(messenger, windowManager);
        MainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        ConfigureOverlayWindow();

        MainViewModel.AreCoordinatesVisible = generalConfig.CurrentValue.ShowCoordinates;
        generalConfig.OnChange(config =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                MainViewModel.AreCoordinatesVisible = config.ShowCoordinates;
            });
        });

        // Initialize and bind managers.
        if (windowManager is DuckWindowManager duckWindowManager)
            duckWindowManager.Initialize(AppWindow);
        
        // Initialize movement manager with screen and duck dimensions
        DisplayArea display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        _movementManager.Initialize(
            display.OuterBounds.Width,
            display.OuterBounds.Height,
            AppWindow.Size.Width,
            AppWindow.Size.Height);

        // Place duck at screen center initially
        double startX = (display.OuterBounds.Width - AppWindow.Size.Width) / 2.0;
        double startY = (display.OuterBounds.Height - AppWindow.Size.Height) / 2.0;
        AppWindow.Move(new Windows.Graphics.PointInt32((int)startX, (int)startY));

        _movementManager.StateChanged += OnDuckStateChanged;
        _movementManager.PositionChanged += OnDuckPositionChanged;
        _movementManager.Start(startX, startY);

        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Listens for ViewModel property changes that require WinUI-specific responses.
    /// When <see cref="MainViewModel.NotificationColorHex"/> changes, creates a
    /// <see cref="SolidColorBrush"/> and applies it to the title TextBlock directly,
    /// because WinUI has no built-in string-to-Brush converter.
    /// </summary>
    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.NotificationColorHex))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                string hex = MainViewModel.NotificationColorHex;
                SpeechBubbleTitle.Foreground = HexToBrush(hex);
            });
        }
    }

    /// <summary>
    /// Marshals DuckState changes from the movement manager onto the UI thread
    /// before updating the ViewModel.
    /// </summary>
    private void OnDuckStateChanged(Core.Enums.DuckState state)
    {
        DispatcherQueue.TryEnqueue(() => MainViewModel.OnDuckStateChanged(state));
    }

    /// <summary>
    /// Marshals position updates from the movement manager onto the UI thread
    /// and physically moves the duck window to the new coordinates.
    /// </summary>
    private void OnDuckPositionChanged(int x, int y)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
            MainViewModel.OnDuckPositionChanged(x, y);
        });
    }

    /// <summary>
    /// Converts a CSS-style hex color string (e.g. "#8B0000") into a WinUI <see cref="SolidColorBrush"/>.
    /// Returns a black brush if parsing fails.
    /// </summary>
    /// <param name="hex">The hex color string to convert.</param>
    private static SolidColorBrush HexToBrush(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Colors.Black);
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            _windowService.UnregisterHotkey(hwnd, _windowService.HotkeyId);
            if (_subclassProc != null)
            {
                _windowService.RemoveSubclass(hwnd, _subclassProc, new IntPtr(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey Cleanup Error");
        }

        try
        {
            if (_movementManager != null)
            {
                _movementManager.StateChanged -= OnDuckStateChanged;
                _movementManager.PositionChanged -= OnDuckPositionChanged;
                MainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Movement Cleanup Error");
        }
    }

    private void ConfigureOverlayWindow()
    {
        SystemBackdrop = new TransparentBackdrop();

        AppWindow appWindow = AppWindow;

        OverlappedPresenter presenter = OverlappedPresenter.CreateForToolWindow();
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(false, false);
        appWindow.SetPresenter(presenter);

        appWindow.Resize(new SizeInt32(300, 300));

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        _windowService.ConfigureOverlayStyles(hwnd);

        try
        {
            _subclassProc = new Win32WindowHelper.SUBCLASSPROC(NewWindowProc);
            _windowService.RegisterSubclass(hwnd, _subclassProc, new IntPtr(1), IntPtr.Zero);
            _windowService.RegisterHotkey(hwnd, _windowService.HotkeyId, _windowService.ModControl | _windowService.ModAlt | _windowService.ModShift, _windowService.VkD);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey Registration Failed");
        }
    }

    private IntPtr NewWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == _windowService.WmHotkey && wParam.ToInt32() == _windowService.HotkeyId)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DisplayArea displayArea = DisplayArea.Primary;
                RectInt32 workArea = displayArea.WorkArea;

                double centerX = workArea.X + (workArea.Width - AppWindow.Size.Width) / 2.0;
                double centerY = workArea.Y + (workArea.Height - AppWindow.Size.Height) / 2.0;

                _movementManager.TeleportTo(centerX, centerY);
            });
            return IntPtr.Zero;
        }

        return _windowService.DefaultSubclassProc(hWnd, uMsg, wParam, lParam);
    }
}
