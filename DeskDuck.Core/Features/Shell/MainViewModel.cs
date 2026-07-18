using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Core.Enums;
using DeskDuck.Core.Messages;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DeskDuck.Core.Features.Shell;

/// <summary>
/// View model for the main overlay window. Exposes bindable properties for the
/// duck animation URI, notification content, notification visibility, coordinate
/// display, and the title bar visibility of notifications.
/// </summary>
public partial class MainViewModel : ObservableObject, IRecipient<ShowNotificationMessage>, IRecipient<HideNotificationMessage>
{
    /// <summary>
    /// Gets or sets the URI of the duck image or animation to display.
    /// </summary>
    [ObservableProperty]
    public partial string DuckImageUri { get; set; } = "ms-appx:///Assets/Duck/duck-sitting.gif";

    /// <summary>
    /// Gets or sets the title text for the currently displayed notification.
    /// </summary>
    [ObservableProperty]
    public partial string NotificationTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the main message text for the currently displayed notification.
    /// </summary>
    [ObservableProperty]
    public partial string NotificationMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the notification speech bubble is visible.
    /// </summary>
    [ObservableProperty]
    public partial Visibility NotificationVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Gets or sets a value indicating whether the notification title block is visible.
    /// </summary>
    [ObservableProperty]
    public partial Visibility TitleVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Gets or sets the brush used for the notification text (color changes based on severity).
    /// </summary>
    [ObservableProperty]
    public partial Brush NotificationTextBrush { get; set; } = new SolidColorBrush(Colors.Black);

    /// <summary>
    /// Gets or sets the formatted text displaying the current X and Y coordinates.
    /// </summary>
    [ObservableProperty]
    public partial string CoordinatesText { get; set; } = "X: 0, Y: 0";

    /// <summary>
    /// Gets or sets a value indicating whether the coordinates overlay is visible.
    /// </summary>
    [ObservableProperty]
    public partial Visibility CoordinatesVisibility { get; set; } = Visibility.Visible;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IMessenger _messenger;
    private readonly IDuckWindowManager _windowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="dispatcherQueue">The UI thread dispatcher queue.</param>
    /// <param name="messenger">The messenger for receiving notifications.</param>
    /// <param name="windowManager">The window manager for handling auxiliary windows.</param>
    public MainViewModel(DispatcherQueue dispatcherQueue, IMessenger messenger, IDuckWindowManager windowManager)
    {
        _dispatcherQueue = dispatcherQueue;
        _messenger = messenger;
        _windowManager = windowManager;
        _messenger.RegisterAll(this);
    }

    /// <summary>
    /// Command to open or focus the chat window.
    /// </summary>
    [RelayCommand]
    private void OpenChat()
    {
        _windowManager.OpenChatWindow();
    }

    /// <summary>
    /// Command to open or focus the settings window.
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        _windowManager.OpenSettingsWindow();
    }

    /// <summary>
    /// Command to exit the application and close all windows.
    /// </summary>
    [RelayCommand]
    private void Exit()
    {
        _windowManager.CloseAll();
        Application.Current.Exit();
    }

    /// <summary>
    /// Handles state changes from the DuckMovementManager to update the duck animation.
    /// </summary>
    /// <param name="state">The new state of the duck.</param>
    public void OnDuckStateChanged(DuckState state)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            DuckImageUri = state switch
            {
                DuckState.WalkingLeft => "ms-appx:///Assets/Duck/duck-walking-to-left.gif",
                DuckState.WalkingRight => "ms-appx:///Assets/Duck/duck-walking-to-right.gif",
                DuckState.Held => "ms-appx:///Assets/Duck/pokeball.gif",
                _ => "ms-appx:///Assets/Duck/duck-sitting.gif"
            };
        });
    }

    partial void OnNotificationTitleChanged(string value)
    {
        TitleVisibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Handles position updates from the DuckMovementManager to update the coordinates display.
    /// </summary>
    /// <param name="x">The new X coordinate.</param>
    /// <param name="y">The new Y coordinate.</param>
    public void OnDuckPositionChanged(int x, int y)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            CoordinatesText = $"X: {x}, Y: {y}";
        });
    }

    /// <inheritdoc/>
    public void Receive(ShowNotificationMessage message)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            NotificationTitle = message.Notification.Title ?? string.Empty;
            NotificationMessage = message.Notification.Message;

            // Map severity to colors
            var severity = message.Notification.Severity?.ToLowerInvariant() ?? "";
            NotificationTextBrush = severity switch
            {
                "warning" => new SolidColorBrush(Colors.DarkRed),
                "info" => new SolidColorBrush(Colors.DarkBlue),
                _ => new SolidColorBrush(Colors.Black)
            };

            NotificationVisibility = Visibility.Visible;
        });
    }

    /// <inheritdoc/>
    public void Receive(HideNotificationMessage message)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            NotificationVisibility = Visibility.Collapsed;
        });
    }
}
