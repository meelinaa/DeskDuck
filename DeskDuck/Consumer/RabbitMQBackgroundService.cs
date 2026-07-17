using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeskDuck.Models;
using Microsoft.UI.Dispatching;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DeskDuck.Consumer
{
    /// <summary>
    /// Background service that maintains a persistent RabbitMQ consumer connection and
    /// dispatches incoming notification messages to the UI thread.
    /// Automatically reconnects on connection failures with a 5-second retry delay.
    /// </summary>
    public class RabbitMQBackgroundService(
        DispatcherQueue dispatcherQueue,
        Action<NotificationMessage> showNotification,
        Action hideNotification,
        RabbitMqOptions rabbitMqOptions)
    {
        private readonly DispatcherQueue _dispatcherQueue = dispatcherQueue;
        private readonly Action<NotificationMessage> _showNotification = showNotification;
        private readonly Action _hideNotification = hideNotification;
        private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions;
        private CancellationTokenSource? _cts;
        private IConnection? _connection;
        private IChannel? _channel;

        private readonly string _queue = rabbitMqOptions.QueueName;

        /// <summary>
        /// Starts the background listener by firing off the consumer loop on a thread-pool thread.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenForNotificationsAsync(_cts.Token));
        }

        /// <summary>
        /// Signals the consumer loop to stop and gracefully closes the channel and connection.
        /// </summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();
            await CleanupRabbitMqResourcesAsync();
        }

        /// <summary>
        /// Gracefully closes and disposes the connection and channel if they exist.
        /// </summary>
        private async Task CleanupRabbitMqResourcesAsync()
        {
            if (_channel != null)
            {
                try
                {
                    if (_channel.IsOpen)
                    {
                        await _channel.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RabbitMQ Cleanup] Error closing channel: {ex.Message}");
                }
                finally
                {
                    _channel.Dispose();
                    _channel = null;
                }
            }

            if (_connection != null)
            {
                try
                {
                    if (_connection.IsOpen)
                    {
                        await _connection.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RabbitMQ Cleanup] Error closing connection: {ex.Message}");
                }
                finally
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
        }

        /// <summary>
        /// Core consumer loop that connects to RabbitMQ, declares the notification queue,
        /// and processes messages one at a time. Each message is displayed for 30 seconds
        /// before being acknowledged so RabbitMQ does not deliver the next one prematurely.
        /// Uses prefetch count of 1 to guarantee sequential, non-overlapping notifications.
        /// </summary>
        private async Task ListenForNotificationsAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _rabbitMqOptions.HostName,
                UserName = _rabbitMqOptions.UserName,
                Password = _rabbitMqOptions.Password
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupRabbitMqResourcesAsync();

                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                    await _channel.QueueDeclareAsync(
                        queue: _queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken);

                    await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

                    var consumer = new AsyncEventingBasicConsumer(_channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var messageJson = Encoding.UTF8.GetString(body);
                            var notification = JsonSerializer.Deserialize<NotificationMessage>(messageJson, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (notification != null)
                            {
                                _dispatcherQueue.TryEnqueue(() =>
                                {
                                    _showNotification(notification);
                                });

                                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                                _dispatcherQueue.TryEnqueue(() =>
                                {
                                    _hideNotification();
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing message: {ex.Message}");
                        }
                        finally
                        {
                            try
                            {
                                if (_channel != null && _channel.IsOpen)
                                {
                                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error sending BasicAck: {ex.Message}");
                            }
                        }
                    };

                    await _channel.BasicConsumeAsync(
                        queue: _queue,
                        autoAck: false,
                        consumer: consumer,
                        cancellationToken: cancellationToken);

                    while (_connection.IsOpen && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RabbitMQ connection failed: {ex.Message}. Retrying in 5 seconds...");
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }
    }
}
