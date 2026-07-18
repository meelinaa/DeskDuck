using DeskDuck.Core.Features.Shell;
using DeskDuck.Core.Helper;
using DeskDuck.Core.Manager;
using DeskDuck.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using Windows.Graphics;
using WinRT.Interop;

namespace DeskDuck.Features.Shell;

/// <summary>
/// Transparent overlay window that displays a walking duck on the desktop.
/// The window is click-through so you can interact with apps underneath.
/// </summary>
public sealed partial class MainWindow : Window
{
    private Win32WindowHelper.SUBCLASSPROC? _subclassProc;

    private readonly IDuckMovementManager _movementManager;
    private readonly ILogger<MainWindow> _logger;
    private readonly IWindowService _windowService;

    /// <summary>
    /// Gets the view model bound to this window.
    /// </summary>
    public MainViewModel MainViewModel { get; }

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
        IDuckMovementManager movementManager,
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

        MainViewModel = new MainViewModel(DispatcherQueue, messenger, windowManager);

        ConfigureOverlayWindow();

        MainViewModel.CoordinatesVisibility = generalConfig.CurrentValue.ShowCoordinates ? Visibility.Visible : Visibility.Collapsed;
        generalConfig.OnChange(config =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                MainViewModel.CoordinatesVisibility = config.ShowCoordinates ? Visibility.Visible : Visibility.Collapsed;
            });
        });

        // Initialize and bind managers
        windowManager.Initialize(AppWindow);
        
        _movementManager.Initialize(AppWindow, DispatcherQueue);
        _movementManager.StateChanged += MainViewModel.OnDuckStateChanged;
        _movementManager.PositionChanged += MainViewModel.OnDuckPositionChanged;
        _movementManager.Start();

        Closed += OnWindowClosed;
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
                _movementManager.StateChanged -= MainViewModel.OnDuckStateChanged;
                _movementManager.PositionChanged -= MainViewModel.OnDuckPositionChanged;
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
