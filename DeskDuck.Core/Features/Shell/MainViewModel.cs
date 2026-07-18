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
    [ObservableProperty]
    public partial string DuckImageUri { get; set; } = "ms-appx:///Assets/Duck/duck-sitting.gif";

    [ObservableProperty]
    public partial string NotificationTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NotificationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility NotificationVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility TitleVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Brush NotificationTextBrush { get; set; } = new SolidColorBrush(Colors.Black);

    [ObservableProperty]
    public partial string CoordinatesText { get; set; } = "X: 0, Y: 0";

    [ObservableProperty]
    public partial Visibility CoordinatesVisibility { get; set; } = Visibility.Visible;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IMessenger _messenger;
    private readonly IDuckWindowManager _windowManager;

    public MainViewModel(DispatcherQueue dispatcherQueue, IMessenger messenger, IDuckWindowManager windowManager)
    {
        _dispatcherQueue = dispatcherQueue;
        _messenger = messenger;
        _windowManager = windowManager;
        _messenger.RegisterAll(this);
    }

    [RelayCommand]
    private void OpenChat()
    {
        _windowManager.OpenChatWindow();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManager.OpenSettingsWindow();
    }

    [RelayCommand]
    private void Exit()
    {
        _windowManager.CloseAll();
        Application.Current.Exit();
    }

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

    public void OnDuckPositionChanged(int x, int y)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            CoordinatesText = $"X: {x}, Y: {y}";
        });
    }

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

    public void Receive(HideNotificationMessage message)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            NotificationVisibility = Visibility.Collapsed;
        });
    }
}
