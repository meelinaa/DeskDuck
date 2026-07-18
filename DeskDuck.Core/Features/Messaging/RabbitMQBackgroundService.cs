using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Core.Messages;
using DeskDuck.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DeskDuck.Core.Features.Messaging;

/// <summary>
/// Background service that maintains a persistent RabbitMQ consumer connection and
/// dispatches incoming notification messages to the UI via IMessenger.
/// Automatically reconnects on connection failures with a 5-second retry delay.
/// </summary>
public partial class RabbitMQBackgroundService : BackgroundService
{
    private readonly IOptionsMonitor<RabbitMqOptions> _optionsMonitor;
    private readonly ILogger<RabbitMQBackgroundService> _logger;
    private readonly IMessenger _messenger;
    private IConnection? _connection;
    private IChannel? _channel;
    private CancellationTokenSource? _reconnectCts;

    public RabbitMQBackgroundService(
        IOptionsMonitor<RabbitMqOptions> optionsMonitor,
        ILogger<RabbitMQBackgroundService> logger,
        IMessenger messenger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _messenger = messenger;

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
                    await _channel.CloseAsync();
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
                    await _connection.CloseAsync();
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
            RabbitMqOptions config = _optionsMonitor.CurrentValue;
            ConnectionFactory factory = new()
            {
                HostName = config.HostName,
                UserName = config.UserName,
                Password = config.Password
            };

            try
            {
                _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                CancellationToken token = _reconnectCts.Token;

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

                AsyncEventingBasicConsumer consumer = new(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        string messageJson = Encoding.UTF8.GetString(body);
                        NotificationMessage? notification = JsonSerializer.Deserialize<NotificationMessage>(messageJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (notification != null)
                        {
                            _messenger.Send(new ShowNotificationMessage(notification));
                            await Task.Delay(TimeSpan.FromSeconds(30), token);
                            _messenger.Send(new HideNotificationMessage());
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
                                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: token);
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
                try { await Task.Delay(5000, stoppingToken); } 
                catch 
                {
                    // Ignore cancellation 
                }
            }
            finally
            {
                _reconnectCts?.Dispose();
                _reconnectCts = null;
            }
        }
    }
}
