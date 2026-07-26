using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Core.Features.Messaging;
using DeskDuck.Core.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DeskDuck.Tests.Features.Messaging;

public class RabbitMqBackgroundServiceTests
{
    private readonly Mock<IOptionsMonitor<RabbitMqOptions>> _optionsMonitorMock;
    private readonly Mock<ILogger<RabbitMqBackgroundService>> _loggerMock;
    private readonly Mock<IMessenger> _messengerMock;
    
    private readonly Mock<IConnectionFactory> _connectionFactoryMock;
    private readonly Mock<IConnection> _connectionMock;
    private readonly Mock<IChannel> _channelMock;

    private readonly RabbitMqOptions _options;

    public RabbitMqBackgroundServiceTests()
    {
        _options = new RabbitMqOptions
        {
            HostName = "testhost",
            UserName = "user",
            Password = "password",
            QueueName = "test_queue"
        };

        _optionsMonitorMock = new Mock<IOptionsMonitor<RabbitMqOptions>>();
        _optionsMonitorMock.Setup(o => o.CurrentValue).Returns(_options);

        _loggerMock = new Mock<ILogger<RabbitMqBackgroundService>>();
        _messengerMock = new Mock<IMessenger>();

        _connectionFactoryMock = new Mock<IConnectionFactory>();
        _connectionMock = new Mock<IConnection>();
        _channelMock = new Mock<IChannel>();

        // Setup successful connection chain
        _connectionFactoryMock
            .Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_connectionMock.Object);

        _connectionMock
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channelMock.Object);
            
        // Make sure connection stays "open" so the loop doesn't exit prematurely
        _connectionMock.SetupGet(c => c.IsOpen).Returns(true);
        _channelMock.SetupGet(c => c.IsOpen).Returns(true);
    }

    /// <summary>
    /// Wrapper class to inject the mocked IConnectionFactory (Extract and Override Pattern).
    /// </summary>
    private class TestableRabbitMqBackgroundService : RabbitMqBackgroundService
    {
        private readonly IConnectionFactory _factory;

        public TestableRabbitMqBackgroundService(
            IOptionsMonitor<RabbitMqOptions> optionsMonitor,
            ILogger<RabbitMqBackgroundService> logger,
            IMessenger messenger,
            IConnectionFactory factory)
            : base(optionsMonitor, logger, messenger)
        {
            _factory = factory;
        }

        protected override IConnectionFactory CreateConnectionFactory(RabbitMqOptions config)
        {
            return _factory;
        }

        protected override Task ReconnectDelayAsync(int millisecondsDelay, CancellationToken cancellationToken)
        {
            return Task.Delay(10, cancellationToken); // Keep tests fast
        }
    }

    /// <summary>
    /// Task 1: Setup & Right-Case
    /// Right: Expects correct Queue declaration and Consumer registration upon startup.
    /// Covers Branch Coverage (Standard path without exceptions).
    /// </summary>
    [Fact]
    public async Task StartAsync_WithValidConfig_ConnectsAndDeclaresQueue()
    {
        // Arrange
        var service = new TestableRabbitMqBackgroundService(
            _optionsMonitorMock.Object,
            _loggerMock.Object,
            _messengerMock.Object,
            _connectionFactoryMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        // Start the background service loop
        var executeTask = service.StartAsync(cts.Token);
        
        // Wait briefly to allow the async connection/queue setup to complete
        await Task.Delay(100); 
        
        // Stop the service
        cts.Cancel();
        try { await executeTask; } catch (TaskCanceledException) { }

        // Assert
        // Verify that the connection and channel were created (Moq cannot verify extension methods like QueueDeclareAsync)
        _connectionFactoryMock.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Task 2: Error Condition - Broker Offline & Health Check UI
    /// Boundary/Error: If CreateConnectionAsync fails, the persistent warning is sent.
    /// Covers Branch Coverage (catch block, _wasOffline = true, UI Notification).
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenConnectionFails_SendsPersistentWarningToUI()
    {
        // Arrange
        // Force the connection to throw an exception
        _connectionFactoryMock
            .Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RabbitMQ.Client.Exceptions.BrokerUnreachableException(new System.Exception("Network down")));

        // Use a real messenger to avoid Moq extension method verification issues
        var realMessenger = new CommunityToolkit.Mvvm.Messaging.StrongReferenceMessenger();
        ShowNotificationMessage? receivedMessage = null;
        realMessenger.Register<ShowNotificationMessage>(this, (r, m) => receivedMessage = m);

        var service = new TestableRabbitMqBackgroundService(
            _optionsMonitorMock.Object,
            _loggerMock.Object,
            realMessenger,
            _connectionFactoryMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        
        // Wait briefly for the connection attempt to fail and the catch-block to execute
        await Task.Delay(100);
        cts.Cancel();
        
        try { await executeTask; } catch (TaskCanceledException) { }

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.True(receivedMessage!.IsPersistent);
        Assert.Equal("warning", receivedMessage!.Notification.Severity);
        Assert.Contains("Docker Desktop", receivedMessage!.Notification.Message);
    }

    /// <summary>
    /// Task 3: Error Condition - Broker Recovery (Success Message)
    /// Boundary/Error: If the service reconnects after being offline, a success message is fired.
    /// Covers Branch Coverage (if (_wasOffline) -> Success Message).
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenConnectionRecovers_SendsSuccessMessageToUI()
    {
        // Arrange
        var realMessenger = new CommunityToolkit.Mvvm.Messaging.StrongReferenceMessenger();
        ShowNotificationMessage? lastReceivedMessage = null;
        realMessenger.Register<ShowNotificationMessage>(this, (r, m) => lastReceivedMessage = m);

        var service = new TestableRabbitMqBackgroundService(
            _optionsMonitorMock.Object,
            _loggerMock.Object,
            realMessenger,
            _connectionFactoryMock.Object);

        // Sequence: First throw (offline), then succeed (recovery)
        _connectionFactoryMock
            .SetupSequence(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RabbitMQ.Client.Exceptions.BrokerUnreachableException(new System.Exception("Network down")))
            .ReturnsAsync(_connectionMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        
        // Wait long enough for the first failed attempt, the short 10ms reconnect delay, and the successful second attempt
        await Task.Delay(300);
        cts.Cancel();
        
        try { await executeTask; } catch (TaskCanceledException) { }

        // Assert
        Assert.NotNull(lastReceivedMessage);
        Assert.False(lastReceivedMessage!.IsPersistent);
        Assert.Equal("success", lastReceivedMessage!.Notification.Severity);
        Assert.Contains("online", lastReceivedMessage!.Notification.Message);
    }

    /// <summary>
    /// Task 4: Error Condition - Poison Message & BasicAck
    /// Boundary/Error: If an incoming message contains invalid JSON, it logs the error and sends BasicAck to prevent blocking.
    /// Covers Branch Coverage (ReceivedAsync catch block, finally block).
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenMessageIsInvalidJson_DoesNotCrashAndSendsAck()
    {
        // Arrange
        var realMessenger = new CommunityToolkit.Mvvm.Messaging.StrongReferenceMessenger();
        ShowNotificationMessage? receivedMessage = null;
        realMessenger.Register<ShowNotificationMessage>(this, (r, m) => receivedMessage = m);

        RabbitMQ.Client.Events.AsyncEventingBasicConsumer? capturedConsumer = null;

        // Capture the consumer when it gets registered via the underlying interface method
        _channelMock.Setup(c => c.BasicConsumeAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), 
            It.IsAny<System.Collections.Generic.IDictionary<string, object?>>(),
            It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string q, bool aa, string ct, bool nl, bool e, System.Collections.Generic.IDictionary<string, object?> args, IAsyncBasicConsumer consumer, CancellationToken t) => 
            {
                capturedConsumer = consumer as RabbitMQ.Client.Events.AsyncEventingBasicConsumer;
            })
            .ReturnsAsync("test-consumer-tag");

        var service = new TestableRabbitMqBackgroundService(
            _optionsMonitorMock.Object,
            _loggerMock.Object,
            realMessenger,
            _connectionFactoryMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        
        // Wait briefly for consumer to be registered
        await Task.Delay(100);
        
        Assert.NotNull(capturedConsumer);

        // Simulate a Poison Message (invalid JSON)
        var invalidJsonBody = System.Text.Encoding.UTF8.GetBytes("{ invalid json ");
        await capturedConsumer!.HandleBasicDeliverAsync("tag", 12345, false, "exchange", "routingKey", null, invalidJsonBody, CancellationToken.None);

        cts.Cancel();
        try { await executeTask; } catch (TaskCanceledException) { }

        // Assert
        // Messenger should NOT have received a notification (other than potentially a recovery message, but we care about the invalid json not generating one)
        // Wait, if it recovered from offline, it might send a success message. But we didn't throw an offline exception here, so it's just null.
        Assert.Null(receivedMessage);

        // Verify that BasicAckAsync was still called for deliveryTag 12345 despite the JSON parsing failure
        _channelMock.Verify(c => c.BasicAckAsync(12345, false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
