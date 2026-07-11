using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DeskDuck
{
    public class NotificationMessage
    {
        public string? Title { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RabbitMQBackgroundService
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly Action<NotificationMessage> _showNotification;
        private readonly Action _hideNotification;
        private CancellationTokenSource? _cts;
        private IConnection? _connection;
        private IChannel? _channel;

        public RabbitMQBackgroundService(
            DispatcherQueue dispatcherQueue,
            Action<NotificationMessage> showNotification,
            Action hideNotification)
        {
            _dispatcherQueue = dispatcherQueue;
            _showNotification = showNotification;
            _hideNotification = hideNotification;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenForNotificationsAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_channel != null)
            {
                await _channel.CloseAsync();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
            }
        }

        private async Task ListenForNotificationsAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "deskduck",
                Password = "deskduck"
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                    // Declare queue (durable = true)
                    await _channel.QueueDeclareAsync(
                        queue: "deskduck.notifications",
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken);

                    // Prefetch count = 1 guarantees sequential processing.
                    // RabbitMQ won't deliver a new message until the current one is acknowledged (Acked).
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
                                // 1. Display notification on the UI thread
                                _dispatcherQueue.TryEnqueue(() =>
                                {
                                    _showNotification(notification);
                                });

                                // 2. Wait 30 seconds
                                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                                // 3. Hide notification on the UI thread
                                _dispatcherQueue.TryEnqueue(() =>
                                {
                                    _hideNotification();
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error processing message: {ex.Message}");
                        }
                        finally
                        {
                            // Acknowledge message only AFTER the 30-second delay has passed.
                            // This signals RabbitMQ to deliver the next message.
                            await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                        }
                    };

                    await _channel.BasicConsumeAsync(
                        queue: "deskduck.notifications",
                        autoAck: false, // Manual Ack is required for our sequential behavior
                        consumer: consumer,
                        cancellationToken: cancellationToken);

                    // Starte den automatischen Test-Nachrichten-Publisher
                    _ = StartTestPublishingLoopAsync(_channel, cancellationToken);

                    // Keep the task alive while connection is open
                    while (_connection.IsOpen && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RabbitMQ connection failed: {ex.Message}. Retrying in 5 seconds...");
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        private async Task StartTestPublishingLoopAsync(IChannel channel, CancellationToken cancellationToken)
        {
            int testCount = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Warte 2 Minuten
                    await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);

                    testCount++;
                    var testMessage = new NotificationMessage
                    {
                        Title = $"DeskDuck Test #{testCount}",
                        Message = $"Dies ist eine automatisch gesendete Testnachricht um {DateTime.Now:HH:mm:ss}."
                    };

                    var json = JsonSerializer.Serialize(testMessage);
                    var body = Encoding.UTF8.GetBytes(json);

                    // Veröffentliche die Nachricht in der Queue
                    await channel.BasicPublishAsync(
                        exchange: string.Empty,
                        routingKey: "deskduck.notifications",
                        body: body,
                        cancellationToken: cancellationToken);

                    System.Diagnostics.Debug.WriteLine($"[TestPublisher] Sent test message #{testCount} to RabbitMQ");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[TestPublisher] Error publishing test message: {ex.Message}");
                }
            }
        }
    }
}
