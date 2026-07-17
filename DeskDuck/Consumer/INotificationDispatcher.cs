using DeskDuck.Models;

namespace DeskDuck.Consumer
{
    /// <summary>
    /// Abstraction for dispatching notification messages to the UI layer.
    /// Decouples the RabbitMQ consumer from the concrete MainWindow implementation,
    /// enabling the consumer to be registered as a hosted service without a direct
    /// reference to any UI class.
    /// </summary>
    public interface INotificationDispatcher
    {
        /// <summary>
        /// Displays the given notification in the UI.
        /// Implementations must marshal the call to the UI thread if required.
        /// </summary>
        void Show(NotificationMessage message);

        /// <summary>
        /// Hides the currently displayed notification from the UI.
        /// Implementations must marshal the call to the UI thread if required.
        /// </summary>
        void Hide();
    }
}
