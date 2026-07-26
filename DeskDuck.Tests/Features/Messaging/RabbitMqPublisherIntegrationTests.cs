using System.Text;
using System.Text.Json;
using DeskDuck.Core.Models;
using DeskDuck.Core.Features.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace DeskDuck.Tests.Features.Messaging;

public class RabbitMqPublisherIntegrationTests : IAsyncLifetime
{
    private RabbitMqContainer? _rabbitMqContainer;
    private IConnection? _testConsumerConnection;
    private IChannel? _testConsumerChannel;
    private bool _dockerAvailable = true;
    private string _dockerSkipReason = string.Empty;

    public RabbitMqPublisherIntegrationTests()
    {
    }

    public async Task InitializeAsync()
    {
        try
        {
            _rabbitMqContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:3-management")
                .WithUsername("test_user")
                .WithPassword("test_pass")
                .Build();

            await _rabbitMqContainer.StartAsync();

            // Setup a direct connection to consume messages and assert they arrived
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqContainer.Hostname,
                Port = _rabbitMqContainer.GetMappedPublicPort(5672),
                UserName = "test_user",
                Password = "test_pass"
            };
            _testConsumerConnection = await factory.CreateConnectionAsync();
            _testConsumerChannel = await _testConsumerConnection.CreateChannelAsync();
        }
        catch (Exception ex)
        {
            // Catch DockerUnavailableException or DockerImageNotFoundException
            // (e.g. GitHub Actions Windows runners don't support Linux containers)
            _dockerAvailable = false;
            _dockerSkipReason = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_testConsumerChannel != null)
            await _testConsumerChannel.CloseAsync();
        if (_testConsumerConnection != null)
            await _testConsumerConnection.CloseAsync();
            
        if (_rabbitMqContainer != null)
            await _rabbitMqContainer.DisposeAsync();
    }

    /// <summary>
    /// Tests that the publisher successfully sends a valid NotificationMessage to a real RabbitMQ broker.
    /// Covers true integration spanning serialization, network transit, and broker acceptance.
    /// </summary>
    // [R]IGHT: The message is correctly published to and readable from the expected queue
    // [C]ROSS-CHECK: We use an independent consumer channel to verify the publisher's output
    [Fact]
    public async Task PublishAsync_PublishesMessageToRealQueue()
    {
        if (!_dockerAvailable) return;

        // Arrange
        var queueName = "test.integration.queue";
        var options = new RabbitMqOptions
        {
            HostName = _rabbitMqContainer!.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "test_user",
            Password = "test_pass",
            QueueName = queueName
        };

        var mockOptions = new Mock<IOptionsMonitor<RabbitMqOptions>>();
        mockOptions.Setup(o => o.CurrentValue).Returns(options);
        var mockLogger = new Mock<ILogger<RabbitMqPublisher>>();

        var publisher = new RabbitMqPublisher(mockOptions.Object, mockLogger.Object);

        // Act
        await publisher.PublishAsync("IntegrationTest", "High", "Hello from Testcontainers!", "https://example.com");

        // Assert
        // Read the message from the queue directly using our test consumer
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        bool messageReceived = false;
        try 
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await _testConsumerChannel!.BasicGetAsync(queueName, autoAck: true, cancellationToken: cts.Token);
                if (result != null)
                {
                    var body = result.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var message = JsonSerializer.Deserialize<NotificationMessage>(json);

                    Assert.NotNull(message);
                    Assert.Equal("IntegrationTest", message.Source);
                    Assert.Equal("High", message.Severity);
                    Assert.Equal("Hello from Testcontainers!", message.Text);
                    Assert.Equal("https://example.com", message.Link);

                    messageReceived = true;
                    break;
                }

                // Small delay to prevent spinning the CPU aggressively
                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if timeout hits before we get the message. We assert on messageReceived below.
        }

        Assert.True(messageReceived, "Failed to receive the message within the 5-second timeout. The message was either not published, or lost.");
    }

    /// <summary>
    /// Tests that the publisher successfully handles empty strings and null values for optional fields.
    /// Covers boundary conditions like empty string severity or null link.
    /// </summary>
    // [B]OUNDARY: Empty strings and nulls are processed and serialized correctly
    [Fact]
    public async Task PublishAsync_WithBoundaryValues_PublishesSuccessfully()
    {
        if (!_dockerAvailable) return;

        // Arrange
        var queueName = "test.integration.boundary";
        var options = new RabbitMqOptions
        {
            HostName = _rabbitMqContainer!.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "test_user",
            Password = "test_pass",
            QueueName = queueName
        };

        var mockOptions = new Mock<IOptionsMonitor<RabbitMqOptions>>();
        mockOptions.Setup(o => o.CurrentValue).Returns(options);
        var mockLogger = new Mock<ILogger<RabbitMqPublisher>>();

        var publisher = new RabbitMqPublisher(mockOptions.Object, mockLogger.Object);

        // Act - Boundary values (empty strings, null)
        await publisher.PublishAsync("", "", "", null);

        // Assert
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        bool messageReceived = false;
        try 
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await _testConsumerChannel!.BasicGetAsync(queueName, autoAck: true, cancellationToken: cts.Token);
                if (result != null)
                {
                    var body = result.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var message = JsonSerializer.Deserialize<NotificationMessage>(json);

                    Assert.NotNull(message);
                    Assert.Equal("", message.Source);
                    Assert.Equal("", message.Severity);
                    Assert.Equal("", message.Text);
                    Assert.Null(message.Link);

                    messageReceived = true;
                    break;
                }
                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException) { }

        Assert.True(messageReceived, "Failed to receive boundary test message.");
    }

    /// <summary>
    /// Tests that the publisher handles connection failures (e.g. invalid port/credentials) gracefully without crashing.
    /// </summary>
    // [E]RROR CONDITIONS: When RabbitMQ configuration is completely invalid, the publisher logs the error but does not throw.
    [Fact]
    public async Task PublishAsync_WithInvalidCredentials_DoesNotCrash()
    {
        if (!_dockerAvailable) return;

        // Arrange
        var options = new RabbitMqOptions
        {
            HostName = _rabbitMqContainer!.Hostname,
            Port = 9999, // Invalid port
            UserName = "wrong_user",
            Password = "wrong_password",
            QueueName = "test.integration.error"
        };

        var mockOptions = new Mock<IOptionsMonitor<RabbitMqOptions>>();
        mockOptions.Setup(o => o.CurrentValue).Returns(options);
        var mockLogger = new Mock<ILogger<RabbitMqPublisher>>();

        var publisher = new RabbitMqPublisher(mockOptions.Object, mockLogger.Object);

        // Act & Assert
        // We do not assert an exception here because the publisher is designed to swallow it (Fail-safe).
        // If it throws, the test will fail, which validates our assumption that it doesn't.
        var exception = await Record.ExceptionAsync(() => publisher.PublishAsync("ErrorTest", "High", "This should not crash"));
        
        Assert.Null(exception); // Should be null because errors are caught and logged
    }
}
