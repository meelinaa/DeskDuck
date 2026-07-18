using DeskDuck.Core.Features.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DeskDuck.Tests.Features.Messaging
{
    public class RabbitMqPublisherTests
    {
        private readonly Mock<IOptionsMonitor<RabbitMqOptions>> _mockOptions;
        private readonly Mock<ILogger<RabbitMqPublisher>> _mockLogger;

        public RabbitMqPublisherTests()
        {
            _mockOptions = new Mock<IOptionsMonitor<RabbitMqOptions>>();
            _mockLogger = new Mock<ILogger<RabbitMqPublisher>>();

            RabbitMqOptions config = new() 
            {
                HostName = "invalid.local", // Use an invalid host to intentionally fail connection
                UserName = "test",
                Password = "test",
                QueueName = "test.queue"
            };
            _mockOptions.Setup(o => o.CurrentValue).Returns(config);
        }

        [Fact]
        public async Task PublishAsync_WhenConnectionFails_CatchesExceptionAndLogsError()
        {
            // Arrange
            RabbitMqPublisher publisher = new(_mockOptions.Object, _mockLogger.Object);

            // Act
            // This will try to connect to invalid.local, which will fail and throw an exception.
            // The exception should be caught inside PublishAsync and logged.
            await publisher.PublishAsync("Source", "Info", "Message", null, CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error publishing message")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public void OnChange_TriggeredByOptionsMonitor_DoesNotCrash()
        {
            // Arrange
            Action<RabbitMqOptions, string>? capturedListener = null;

            _mockOptions.Setup(o => o.OnChange(It.IsAny<Action<RabbitMqOptions, string>>()))
                .Callback<Action<RabbitMqOptions, string>>(listener => capturedListener = listener)
                .Returns(Mock.Of<IDisposable>());

            RabbitMqPublisher publisher = new(_mockOptions.Object, _mockLogger.Object);

            Assert.NotNull(capturedListener);

            // Act & Assert
            // Manually trigger the OnChange event to simulate appsettings.json being saved
            Exception exception = Record.Exception(() => capturedListener.Invoke(_mockOptions.Object.CurrentValue, string.Empty));

            // The event handler is async void (or async Task captured as Action), but we ensure it doesn't immediately crash.
            Assert.Null(exception);
        }

        [Fact]
        public async Task PublishAsync_WhenConfigIsMissing_LogsErrorAndDoesNotCrash()
        {
            // Arrange
            RabbitMqOptions invalidConfig = new() 
            {
                HostName = "", // Empty HostName
                QueueName = ""
            };
            _mockOptions.Setup(o => o.CurrentValue).Returns(invalidConfig);

            RabbitMqPublisher publisher = new(_mockOptions.Object, _mockLogger.Object);

            // Act
            await publisher.PublishAsync("Source", "Info", "Message", null, CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error publishing message")),
                    It.Is<Exception>(e => e.Message.Contains("missing in configuration")),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
    }
}
