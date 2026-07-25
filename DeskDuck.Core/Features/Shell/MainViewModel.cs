using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Core.Enums;
using DeskDuck.Core.Messages;

namespace DeskDuck.Core.Features.Shell;

/// <summary>
/// View model for the main overlay window. Exposes bindable properties for the
/// duck animation URI, notification content, notification visibility, coordinate
/// display, and severity-based color of notification text.
/// All properties use framework-agnostic types so this class has zero WinUI dependencies.
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
    /// Bound via BooleanToVisibilityConverter in the UI layer.
    /// </summary>
    [ObservableProperty]
    public partial bool IsNotificationVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the notification title block is visible.
    /// True when <see cref="NotificationTitle"/> is non-empty.
    /// Bound via BooleanToVisibilityConverter in the UI layer.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTitleVisible { get; set; }

    /// <summary>
    /// Gets or sets the hex color string for the notification title text.
    /// Maps the severity level ("Warning" → dark red, "Info" → dark blue, else black).
    /// The UI layer creates a SolidColorBrush from this value.
    /// </summary>
    [ObservableProperty]
    public partial string NotificationColorHex { get; set; } = "#1A1A1A";

    /// <summary>
    /// Gets or sets the formatted text displaying the current X and Y coordinates.
    /// </summary>
    [ObservableProperty]
    public partial string CoordinatesText { get; set; } = "X: 0, Y: 0";

    /// <summary>
    /// Gets or sets a value indicating whether the coordinates overlay is visible.
    /// Bound via BooleanToVisibilityConverter in the UI layer.
    /// </summary>
    [ObservableProperty]
    public partial bool AreCoordinatesVisible { get; set; } = true;

    private readonly IMessenger _messenger;
    private readonly IDuckWindowManager _windowManager;

    /// <summary>
    /// CancellationTokenSource that controls the 30-second auto-hide timer for the
    /// notification bubble. Cancelled and replaced whenever a new notification arrives.
    /// </summary>
    private CancellationTokenSource? _notificationHideCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="messenger">The messenger for receiving notifications.</param>
    /// <param name="windowManager">The window manager for handling auxiliary windows.</param>
    public MainViewModel(IMessenger messenger, IDuckWindowManager windowManager)
    {
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
        _windowManager.Shutdown();
    }

    /// <summary>
    /// Handles state changes from the DuckMovementManager to update the duck animation.
    /// </summary>
    /// <param name="state">The new state of the duck.</param>
    public void OnDuckStateChanged(DuckState state)
    {
        DuckImageUri = state switch
        {
            DuckState.WalkingLeft => "ms-appx:///Assets/Duck/duck-walking-to-left.gif",
            DuckState.WalkingRight => "ms-appx:///Assets/Duck/duck-walking-to-right.gif",
            DuckState.Held => "ms-appx:///Assets/Duck/pokeball.gif",
            _ => "ms-appx:///Assets/Duck/duck-sitting.gif"
        };
    }

    /// <summary>
    /// Handles position updates from the DuckMovementManager to update the coordinates display.
    /// </summary>
    /// <param name="x">The new X coordinate.</param>
    /// <param name="y">The new Y coordinate.</param>
    public void OnDuckPositionChanged(int x, int y)
    {
        CoordinatesText = $"X: {x}, Y: {y}";
    }

    partial void OnNotificationTitleChanged(string value)
    {
        IsTitleVisible = !string.IsNullOrWhiteSpace(value);
    }

    /// <inheritdoc/>
    public void Receive(ShowNotificationMessage message)
    {
        NotificationTitle = message.Notification.Title ?? string.Empty;
        NotificationMessage = message.Notification.Message;

        // Map severity to a hex color — no WinUI Brush required in the Core layer.
        NotificationColorHex = (message.Notification.Severity?.ToLowerInvariant()) switch
        {
            "warning" => "#8B0000",  // DarkRed
            "info"    => "#00008B",  // DarkBlue
            _         => "#1A1A1A"
        };

        IsNotificationVisible = true;

        // Cancel any running hide-timer from a previous notification
        _notificationHideCts?.Cancel();
        _notificationHideCts?.Dispose();
        _notificationHideCts = new CancellationTokenSource();
        CancellationToken token = _notificationHideCts.Token;

        // Start the 30-second auto-hide timer without blocking the caller
        _ = HideAfterDelayAsync(TimeSpan.FromSeconds(30), token);
    }

    /// <inheritdoc/>
    public void Receive(HideNotificationMessage message) => HideNotification();

    /// <summary>
    /// Waits for <paramref name="delay"/> and then hides the notification bubble,
    /// unless the token is cancelled by a newer notification arriving first.
    /// </summary>
    /// <param name="delay">How long to wait before hiding the notification.</param>
    /// <param name="cancellationToken">Token that cancels this timer when a new notification arrives.</param>
    private async Task HideAfterDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            HideNotification();
        }
        catch (OperationCanceledException)
        {
            // A newer notification cancelled this timer — nothing to do.
        }
    }

    /// <summary>
    /// Resets all notification-related properties to their hidden/empty defaults.
    /// </summary>
    private void HideNotification()
    {
        IsNotificationVisible = false;
        NotificationTitle = string.Empty;
        NotificationMessage = string.Empty;
    }
}
