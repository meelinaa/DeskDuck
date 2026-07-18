namespace DeskDuck.Core.Features.Messaging;

/// <summary>
/// Abstraction for publishing notification messages to a message broker.
/// Decouples consumers (hosted services) from the concrete RabbitMQ implementation.
/// </summary>
public interface IRabbitMqPublisher
{
    /// <summary>
    /// Publishes a notification message asynchronously.
    /// </summary>
    /// <param name="source">The originating component (e.g. "SystemMonitor", "Wetter").</param>
    /// <param name="severity">The severity level (e.g. "Info", "Warning").</param>
    /// <param name="text">The human-readable notification text.</param>
    /// <param name="link">An optional URL associated with the notification.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PublishAsync(
        string source,
        string severity,
        string text,
        string? link = null,
        CancellationToken cancellationToken = default);
}
