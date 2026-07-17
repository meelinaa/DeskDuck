using DeskDuck.Models;

namespace DeskDuck.Messages
{
    /// <summary>
    /// Message broadcasted over the IMessenger when a new notification should be shown.
    /// </summary>
    public record ShowNotificationMessage(NotificationMessage Notification);
}
