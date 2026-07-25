using DeskDuck.Core.Features.Chat;
using DeskDuck.Core.Features.Movement;
using DeskDuck.Core.Features.Shell;
using DeskDuck.Features.Chat;
using DeskDuck.Features.Settings;
using DeskDuck.Core.Features.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using System;
using Windows.Graphics;
using Microsoft.UI.Xaml;

namespace DeskDuck.Features.Shell;

/// <summary>
/// Concrete implementation of <see cref="IDuckWindowManager"/> that manages
/// the lifecycle and positioning of the chat and settings windows.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DuckWindowManager"/> class.
/// </remarks>
/// <param name="serviceProvider">Provider for resolving ViewModels and dependencies.</param>
/// <param name="movementManager">The movement manager to pause/resume the duck.</param>
/// <param name="loggerFactory">Factory for creating loggers.</param>
public class DuckWindowManager(
    IServiceProvider serviceProvider,
    IDuckMovementController movementManager,
    ILoggerFactory loggerFactory) : IDuckWindowManager
{
    private AppWindow? _duckAppWindow;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IDuckMovementController _movementManager = movementManager;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    private ChatWindow? _chatWindow;
    private bool _isChatActive = false;

    private SettingsWindow? _settingsWindow;
    private bool _isSettingsActive = false;

    /// <inheritdoc />
    public void Initialize(AppWindow duckAppWindow)
    {
        _duckAppWindow = duckAppWindow;
    }

    /// <inheritdoc />
    public void OpenChatWindow()
    {
        if (_duckAppWindow == null) return;

        _isChatActive = true;

        if (_chatWindow != null)
        {
            _chatWindow.Activate();
            return;
        }

        ChatViewModel chatViewModel = _serviceProvider.GetRequiredService<ChatViewModel>();
        _chatWindow = new ChatWindow(chatViewModel);
        AppWindow chatAppWindow = _chatWindow.AppWindow;

        chatAppWindow.Changed += ChatAppWindow_Changed;

        _chatWindow.Closed += (s, args) =>
        {
            chatAppWindow.Changed -= ChatAppWindow_Changed;
            _chatWindow = null;
            _isChatActive = false;
            
            if (!_isSettingsActive)
            {
                PointInt32 pos = _duckAppWindow?.Position ?? new PointInt32(0, 0);
                _movementManager.Start(pos.X, pos.Y);
            }
        };

        _movementManager.Stop();
        _chatWindow.Activate();

        SyncDuckPositionToWindow(chatAppWindow);
    }

    private void ChatAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange && _chatWindow != null)
            SyncDuckPositionToWindow(sender);
    }

    /// <inheritdoc />
    public void OpenSettingsWindow()
    {
        if (_duckAppWindow == null) return;

        _isSettingsActive = true;

        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        SettingsViewModel settingsViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
        _settingsWindow = new SettingsWindow(settingsViewModel, _loggerFactory.CreateLogger<SettingsWindow>());
        AppWindow settingsAppWindow = _settingsWindow.AppWindow;

        settingsAppWindow.Changed += SettingsAppWindow_Changed;

        _settingsWindow.Closed += (s, args) =>
        {
            settingsAppWindow.Changed -= SettingsAppWindow_Changed;
            _settingsWindow = null;
            _isSettingsActive = false;
            
            if (!_isChatActive)
            {
                PointInt32 pos = _duckAppWindow?.Position ?? new PointInt32(0, 0);
                _movementManager.Start(pos.X, pos.Y);
            }
        };

        _movementManager.Stop();
        _settingsWindow.Activate();

        SyncDuckPositionToWindow(settingsAppWindow);
    }

    private void SettingsAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange && _settingsWindow != null)
            SyncDuckPositionToWindow(sender);
    }

    private void SyncDuckPositionToWindow(AppWindow targetWindow)
    {
        if (_duckAppWindow == null) return;

        PointInt32 pos = targetWindow.Position;
        int newX = pos.X - (_duckAppWindow.Size.Width / 2);
        int newY = pos.Y - (_duckAppWindow.Size.Height / 2);

        _movementManager.TeleportTo(newX, newY);
    }

    /// <inheritdoc />
    public void CloseAll()
    {
        _chatWindow?.Close();
        _settingsWindow?.Close();
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        CloseAll();
        Application.Current.Exit();
    }
}
