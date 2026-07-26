using DeskDuck.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text.Json;

namespace DeskDuck.Core.Features.Messaging;

/// <summary>
/// Singleton RabbitMQ publisher that sends <see cref="NotificationMessage"/> payloads
/// to the <c>deskduck.notifications</c> queue. Connection and channel are created lazily
/// on the first publish and reused for all subsequent calls. A semaphore ensures that
/// concurrent publishers do not race to (re-)establish the connection.
/// </summary>
public partial class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IOptionsMonitor<RabbitMqOptions> _optionsMonitor;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    // Semaphore prevents race conditions and redundant connection attempts during concurrent publishing.
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

    /// <summary>
    /// Initializes the publisher with the injected RabbitMQ options.
    /// </summary>
    public RabbitMqPublisher(
        IOptionsMonitor<RabbitMqOptions> optionsMonitor,
        ILogger<RabbitMqPublisher> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        _optionsMonitor.OnChange(async config =>
        {
            // Force a reconnect on the next publish
            await _connectionSemaphore.WaitAsync();
            try
            {
                if (_channel != null)
                {
                    try
                    {
                        await _channel.CloseAsync();
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogTrace(ex, "Failed to close channel"); 
                    }
                    _channel = null;
                }
                if (_connection != null)
                {
                    try
                    {
                        await _connection.CloseAsync();
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogTrace(ex, "Failed to close connection"); 
                    }
                    _connection = null;
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        });
    }

    /// <summary>
    /// Ensures that an open connection and channel exist before publishing.
    /// Uses a double-checked lock via <see cref="SemaphoreSlim"/> to avoid redundant
    /// reconnect attempts from concurrent callers. Disposes any stale connection first.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
            return;

        await _connectionSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                return;

            if (_channel != null)
            {
                try
                {
                    await _channel.CloseAsync(cancellationToken: cancellationToken);
                }
                catch (Exception ex) 
                { 
                    _logger.LogTrace(ex, "Failed to close channel"); 
                }
                _channel = null;
            }
            if (_connection != null)
            {
                try
                {
                    await _connection.CloseAsync(cancellationToken: cancellationToken);
                }
                catch (Exception ex) 
                { 
                    _logger.LogTrace(ex, "Failed to close connection"); 
                }
                _connection = null;
            }

            RabbitMqOptions config = _optionsMonitor.CurrentValue;

            if (string.IsNullOrWhiteSpace(config.HostName) || string.IsNullOrWhiteSpace(config.QueueName))
                throw new InvalidOperationException("RabbitMQ HostName or QueueName is missing in configuration.");

            ConnectionFactory factory = new()
            {
                HostName = config.HostName,
                UserName = config.UserName,
                Password = config.Password
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                queue: config.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Serializes a notification and publishes it to the <c>deskduck.notifications</c> queue.
    /// Automatically establishes the RabbitMQ connection if it is not yet open.
    /// Errors are logged via Serilog and do not propagate to callers
    /// so that a broker outage never crashes the publisher service.
    /// </summary>
    public async Task PublishAsync(string source, string severity, string text, string? link = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureConnectedAsync(cancellationToken);

            // Capture the channel in a local variable to prevent race conditions
            // if IOptionsMonitor.OnChange fires and sets _channel to null concurrently.
            IChannel? channel = _channel ?? throw new InvalidOperationException("Could not establish RabbitMQ channel.");

            RabbitMqOptions config = _optionsMonitor.CurrentValue;

            NotificationMessage message = new()
            {
                Source = source,
                Severity = severity,
                Text = text,
                Link = link
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            BasicProperties properties = new()
            {
                Persistent = true,
                ContentType = MessagingConstants.ContentTypeJson
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: config.QueueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Published message from source {Source} to RabbitMQ", source);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message");
        }
    }

    /// <summary>
    /// Disposes the channel, connection, and semaphore to release all RabbitMQ resources.
    /// </summary>
    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _connectionSemaphore.Dispose();
    }
}
