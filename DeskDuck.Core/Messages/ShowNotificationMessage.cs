using DeskDuck.Core.Models;

namespace DeskDuck.Core.Messages;

/// <summary>
/// Message broadcasted over the IMessenger when a new notification should be shown.
/// </summary>
public record ShowNotificationMessage(NotificationMessage Notification);
