using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Messages;
using DeskDuck.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeskDuck.Features.Messaging
{
    /// <summary>
    /// Background service that maintains a persistent RabbitMQ consumer connection and
    /// dispatches incoming notification messages to the UI via IMessenger.
    /// Automatically reconnects on connection failures with a 5-second retry delay.
    /// </summary>
    public class RabbitMQBackgroundService(
        IOptions<RabbitMqOptions> options) : BackgroundService
    {
        private readonly RabbitMqOptions _rabbitMqOptions = options.Value;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly string _queue = options.Value.QueueName;
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await CleanupRabbitMqResourcesAsync();
            await base.StopAsync(cancellationToken);
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
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _rabbitMqOptions.HostName,
                UserName = _rabbitMqOptions.UserName,
                Password = _rabbitMqOptions.Password
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupRabbitMqResourcesAsync();

                    _connection = await factory.CreateConnectionAsync(stoppingToken);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                    await _channel.QueueDeclareAsync(
                        queue: _queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: stoppingToken);

                    await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

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
                                WeakReferenceMessenger.Default.Send(new ShowNotificationMessage(notification));

                                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                                WeakReferenceMessenger.Default.Send(new HideNotificationMessage());
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
                                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
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
                        cancellationToken: stoppingToken);

                    while (_connection.IsOpen && !stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RabbitMQ connection failed: {ex.Message}. Retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
    }
}
