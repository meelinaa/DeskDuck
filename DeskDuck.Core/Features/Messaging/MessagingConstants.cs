namespace DeskDuck.Core.Features.Messaging;

/// <summary>
/// Defines constants used for messaging and RabbitMQ configuration.
/// </summary>
public static class MessagingConstants
{
    /// <summary>
    /// The default hostname for the RabbitMQ server.
    /// </summary>
    public const string DefaultHostName = "localhost";

    /// <summary>
    /// The default username for authenticating with the RabbitMQ server.
    /// </summary>
    public const string DefaultUserName = "deskduck";

    /// <summary>
    /// The default password for authenticating with the RabbitMQ server.
    /// </summary>
    public const string DefaultPassword = "deskduck";

    /// <summary>
    /// The default queue name from which notifications are consumed.
    /// </summary>
    public const string DefaultQueueName = "deskduck.notifications";

    /// <summary>
    /// The content type string used for JSON messages.
    /// </summary>
    public const string ContentTypeJson = "application/json";
}
