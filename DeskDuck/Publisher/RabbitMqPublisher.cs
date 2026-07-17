using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeskDuck.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DeskDuck.Publisher
{
    /// <summary>
    /// Singleton RabbitMQ publisher that sends <see cref="NotificationMessage"/> payloads
    /// to the <c>deskduck.notifications</c> queue. Connection and channel are created lazily
    /// on the first publish and reused for all subsequent calls. A semaphore ensures that
    /// concurrent publishers do not race to (re-)establish the connection.
    /// </summary>
    public partial class RabbitMqPublisher : IDisposable
    {
        private readonly ConnectionFactory _factory;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

        private readonly string _queueName;

        /// <summary>
        /// Initializes the publisher with the injected RabbitMQ options.
        /// </summary>
        public RabbitMqPublisher(IOptions<RabbitMqOptions> options)
        {
            var config = options.Value;
            _queueName = config.QueueName;
            _factory = new ConnectionFactory()
            {
                HostName = config.HostName,
                UserName = config.UserName,
                Password = config.Password
            };
        }

        /// <summary>
        /// Ensures that an open connection and channel exist before publishing.
        /// Uses a double-checked lock via <see cref="SemaphoreSlim"/> to avoid redundant
        /// reconnect attempts from concurrent callers. Disposes any stale connection first.
        /// </summary>
        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
            {
                return;
            }

            await _connectionSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                {
                    return;
                }

                if (_channel != null)
                {
                    try { await _channel.CloseAsync(cancellationToken: cancellationToken); } catch { }
                    _channel = null;
                }
                if (_connection != null)
                {
                    try { await _connection.CloseAsync(cancellationToken: cancellationToken); } catch { }
                    _connection = null;
                }

                _connection = await _factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await _channel.QueueDeclareAsync(
                    queue: _queueName,
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
        /// Errors are logged via <see cref="Debug.WriteLine"/> and do not propagate to callers
        /// so that a broker outage never crashes the publisher service.
        /// </summary>
        public async Task PublishAsync(string source, string severity, string text, string? link = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken);

                if (_channel == null)
                {
                    throw new InvalidOperationException("Could not establish RabbitMQ channel.");
                }

                var messageObj = new NotificationMessage
                {
                    Source = source,
                    Severity = severity,
                    Text = text,
                    Link = link
                };

                var json = JsonSerializer.Serialize(messageObj);
                var body = Encoding.UTF8.GetBytes(json);

                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _queueName,
                    body: body,
                    cancellationToken: cancellationToken);

                Debug.WriteLine($"[RabbitMqPublisher] Published message from source {source} to RabbitMQ");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RabbitMqPublisher] Error publishing: {ex.Message}");
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
}
