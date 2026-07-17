using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Messages;
using DeskDuck.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    public class RabbitMQBackgroundService : BackgroundService
    {
        private readonly IOptionsMonitor<RabbitMqOptions> _optionsMonitor;
        private readonly ILogger<RabbitMQBackgroundService> _logger;
        private IConnection? _connection;
        private IChannel? _channel;
        private CancellationTokenSource? _reconnectCts;

        public RabbitMQBackgroundService(
            IOptionsMonitor<RabbitMqOptions> optionsMonitor,
            ILogger<RabbitMQBackgroundService> logger)
        {
            _optionsMonitor = optionsMonitor;
            _logger = logger;

            _optionsMonitor.OnChange(config =>
            {
                // Trigger a cancellation to reconnect with the new settings
                _reconnectCts?.Cancel();
            });
        }
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
                    _logger.LogError(ex, "Error closing channel");
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
                    _logger.LogError(ex, "Error closing connection");
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
            while (!stoppingToken.IsCancellationRequested)
            {
                var config = _optionsMonitor.CurrentValue;
                var factory = new ConnectionFactory()
                {
                    HostName = config.HostName,
                    UserName = config.UserName,
                    Password = config.Password
                };

                try
                {
                    _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var token = _reconnectCts.Token;

                    await CleanupRabbitMqResourcesAsync();

                    _connection = await factory.CreateConnectionAsync(token);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: token);

                    await _channel.QueueDeclareAsync(
                        queue: config.QueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: token);

                    await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: token);

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

                                await Task.Delay(TimeSpan.FromSeconds(30), token);

                                WeakReferenceMessenger.Default.Send(new HideNotificationMessage());
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message");
                        }
                        finally
                        {
                            try
                            {
                                if (_channel != null && _channel.IsOpen)
                                {
                                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: token);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending BasicAck");
                            }
                        }
                    };

                    await _channel.BasicConsumeAsync(
                        queue: config.QueueName,
                        autoAck: false,
                        consumer: consumer,
                        cancellationToken: token);

                    while (_connection.IsOpen && !token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Reconnection requested due to options change or application stop
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RabbitMQ connection failed. Retrying in 5 seconds...");
                    try { await Task.Delay(5000, stoppingToken); } catch { }
                }
                finally
                {
                    _reconnectCts?.Dispose();
                    _reconnectCts = null;
                }
            }
        }
    }
}
