using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace DeskDuck
{
    public class RabbitMqPublisher : IDisposable
    {
        private readonly ConnectionFactory _factory;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

        public RabbitMqPublisher()
        {
            _factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "deskduck",
                Password = "deskduck"
            };
        }

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

                // Cleanup old channel/connection if any
                if (_channel != null)
                {
                    try { await _channel.CloseAsync(); } catch { }
                    _channel = null;
                }
                if (_connection != null)
                {
                    try { await _connection.CloseAsync(); } catch { }
                    _connection = null;
                }

                _connection = await _factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await _channel.QueueDeclareAsync(
                    queue: "deskduck.notifications",
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
                    routingKey: "deskduck.notifications",
                    body: body,
                    cancellationToken: cancellationToken);

                System.Diagnostics.Debug.WriteLine($"[RabbitMqPublisher] Published message from source {source} to RabbitMQ");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RabbitMqPublisher] Error publishing: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            _connectionSemaphore.Dispose();
        }
    }
}
