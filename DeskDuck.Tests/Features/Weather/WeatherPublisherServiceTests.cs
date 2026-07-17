using DeskDuck.Features.Messaging;
using DeskDuck.Features.Weather;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DeskDuck.Tests.Features.Weather
{
    public class WeatherPublisherServiceTests
    {
        private readonly Mock<IOptionsMonitor<WeatherPublisherOptions>> _mockOptions;
        private readonly Mock<IRabbitMqPublisher> _mockPublisher;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<WeatherPublisherService>> _mockLogger;

        public WeatherPublisherServiceTests()
        {
            _mockOptions = new Mock<IOptionsMonitor<WeatherPublisherOptions>>();
            _mockPublisher = new Mock<IRabbitMqPublisher>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<WeatherPublisherService>>();

            var config = new WeatherPublisherOptions
            {
                Enabled = true,
                ApiKey = "dummy-api-key",
                OverrideCity = "TestCity",
                IntervalMinutes = 10
            };
            _mockOptions.Setup(o => o.CurrentValue).Returns(config);
        }

        private async Task InvokePublishWeatherUpdateAsync(WeatherPublisherService service, WeatherPublisherOptions config)
        {
            var method = typeof(WeatherPublisherService).GetMethod("PublishWeatherUpdateAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                var task = (Task)method.Invoke(service, new object[] { config, CancellationToken.None })!;
                await task;
            }
        }

        [Fact]
        public async Task PublishWeatherUpdate_WhenApiReturnsData_PublishesWeatherToRabbitMq()
        {
            // Arrange
            var jsonResponse = @"
            {
                ""main"": {
                    ""temp"": 22.5
                },
                ""weather"": [
                    {
                        ""description"": ""Leichter Regen""
                    }
                ]
            }";

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(jsonResponse),
               })
               .Verifiable();

            var httpClient = new HttpClient(handlerMock.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(httpClient);

            var service = new WeatherPublisherService(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            // Act
            await InvokePublishWeatherUpdateAsync(service, _mockOptions.Object.CurrentValue);

            // Assert
            _mockPublisher.Verify(p => p.PublishAsync(
                "Wetter",
                "Info",
                "Aktuelles Wetter in TestCity: 22,5°C, Leichter Regen.",
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishWeatherUpdate_WhenApiKeyIsEmpty_DoesNothing()
        {
            // Arrange
            var config = new WeatherPublisherOptions
            {
                Enabled = true,
                ApiKey = "",
                OverrideCity = "TestCity",
                IntervalMinutes = 10
            };
            _mockOptions.Setup(o => o.CurrentValue).Returns(config);

            var service = new WeatherPublisherService(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            // Act
            await InvokePublishWeatherUpdateAsync(service, _mockOptions.Object.CurrentValue);

            // Assert
            _mockPublisher.Verify(p => p.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockHttpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PublishWeatherUpdate_WhenJsonIsMissingTemp_LogsErrorAndDoesNotPublish()
        {
            // Arrange
            var jsonResponse = @"{ ""main"": { ""humidity"": 50 } }"; // Missing temp

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(jsonResponse),
               })
               .Verifiable();

            var httpClient = new HttpClient(handlerMock.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(httpClient);

            var service = new WeatherPublisherService(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            // Act
            await InvokePublishWeatherUpdateAsync(service, _mockOptions.Object.CurrentValue);

            // Assert
            _mockPublisher.Verify(p => p.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error fetching weather from API")),
                    It.Is<Exception>(e => e.Message.Contains("missing 'main.temp'")),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
    }
}
