using DeskDuck.Core.Features.Messaging;
using DeskDuck.Core.Features.SystemMonitor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DeskDuck.Tests.Features.SystemMonitor
{
    public class SystemMonitorPublisherServiceTests
    {
        private readonly Mock<IOptionsMonitor<SystemMonitorOptions>> _mockOptions;
        private readonly Mock<IRabbitMqPublisher> _mockPublisher;
        private readonly Mock<ILogger<SystemMonitorPublisherService>> _mockLogger;
        private readonly Mock<ISystemMetricsProvider> _mockMetrics;

        public SystemMonitorPublisherServiceTests()
        {
            _mockOptions = new Mock<IOptionsMonitor<SystemMonitorOptions>>();
            _mockPublisher = new Mock<IRabbitMqPublisher>();
            _mockLogger = new Mock<ILogger<SystemMonitorPublisherService>>();
            _mockMetrics = new Mock<ISystemMetricsProvider>();

            SystemMonitorOptions config = new()
            {
                Enabled = true,
                RamWarningEnabled = true,
                RamWarningThresholdPercent = 80,
                CpuWarningEnabled = true,
                CpuWarningThresholdPercent = 90,
                BatteryWarningEnabled = true,
                BatteryWarningThresholdPercent = 20
            };
            _mockOptions.Setup(o => o.CurrentValue).Returns(config);
        }

        // [R]IGHT: Valid usage correctly triggers warning publication
        [Fact]
        public async Task CheckSystemMetrics_WhenRamExceedsThreshold_PublishesWarning()
        {
            // Arrange
            _mockMetrics.Setup(m => m.GetRamUsage()).Returns(85.0); // Exceeds 80
            _mockMetrics.Setup(m => m.GetCpuUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50.0);
            _mockMetrics.Setup(m => m.GetBatteryPercent()).Returns(100.0);

            SystemMonitorPublisherService service = new(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockLogger.Object,
                _mockMetrics.Object);

            // Act
            await service.CheckSystemMetricsAsync(_mockOptions.Object.CurrentValue, CancellationToken.None);

            // Assert
            _mockPublisher.Verify(p => p.PublishAsync(
                "SystemMonitor",
                "Warning",
                It.Is<string>(s => s.Contains("Hohe RAM-Auslastung")),
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // [R]IGHT: Normal usage correctly skips publication
        [Fact]
        public async Task CheckSystemMetrics_WhenMetricsAreNormal_DoesNotPublish()
        {
            // Arrange
            _mockMetrics.Setup(m => m.GetRamUsage()).Returns(50.0); // Under 80
            _mockMetrics.Setup(m => m.GetCpuUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50.0); // Under 90
            _mockMetrics.Setup(m => m.GetBatteryPercent()).Returns(50.0); // Above 20

            SystemMonitorPublisherService service = new(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockLogger.Object,
                _mockMetrics.Object);

            // Act
            await service.CheckSystemMetricsAsync(_mockOptions.Object.CurrentValue, CancellationToken.None);

            // Assert
            _mockPublisher.Verify(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        // [B]OUNDARY: Invalid threshold is clamped
        [Fact]
        public async Task CheckSystemMetrics_WithInvalidThreshold_ClampsToValidRange()
        {
            // Arrange
            SystemMonitorOptions invalidConfig = new() 
            {
                Enabled = true,
                RamWarningEnabled = true,
                RamWarningThresholdPercent = 150, // Invalid, should be clamped to 100
                CpuWarningEnabled = true,
                CpuWarningThresholdPercent = -10, // Invalid, should be clamped to 0
                BatteryWarningEnabled = false
            };

            // If RAM threshold is clamped to 100, then Ram=90 should NOT trigger a warning
            _mockMetrics.Setup(m => m.GetRamUsage()).Returns(90.0);

            // If CPU threshold is clamped to 0, then Cpu=10 should trigger a warning
            _mockMetrics.Setup(m => m.GetCpuUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10.0);

            SystemMonitorPublisherService service = new(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockLogger.Object,
                _mockMetrics.Object);

            // Act
            await service.CheckSystemMetricsAsync(invalidConfig, CancellationToken.None);

            // Assert
            // RAM = 90 < 100, so NO warning for RAM
            _mockPublisher.Verify(p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("RAM-Auslastung")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);

            // CPU = 10 > 0, so YES warning for CPU
            _mockPublisher.Verify(p => p.PublishAsync(
                "SystemMonitor",
                "Warning",
                It.Is<string>(s => s.Contains("Hohe CPU-Auslastung")),
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // [R]IGHT: Background service correctly exits loop upon cancellation
        [Fact]
        public async Task ExecuteAsync_BackgroundLoop_RespectsCancellation()
        {
            // Arrange
            _mockMetrics.Setup(m => m.GetRamUsage()).Returns(50.0);
            _mockMetrics.Setup(m => m.GetCpuUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50.0);
            _mockMetrics.Setup(m => m.GetBatteryPercent()).Returns(50.0);

            SystemMonitorPublisherService service = new(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockLogger.Object,
                _mockMetrics.Object);

            // Act
            CancellationTokenSource cts = new();
            await service.StartAsync(cts.Token);
            
            // Cancel to break the loop
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(service.ExecuteTask?.IsCompleted);
        }
    }
}
