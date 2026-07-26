using DeskDuck.Core.Features.Messaging;
using DeskDuck.Core.Features.Weather;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;

namespace DeskDuck.Tests.Features.Weather;

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

        WeatherPublisherOptions config = new()
        {
            Enabled = true,
            ApiKey = "dummy-api-key",
            OverrideCity = "TestCity",
            IntervalMinutes = 10
        };
        _mockOptions.Setup(o => o.CurrentValue).Returns(config);
    }

    // [R]IGHT: Valid API response is parsed and published correctly
    [Fact]
    public async Task PublishWeatherUpdate_WhenApiReturnsData_PublishesWeatherToRabbitMq()
    {
        // Arrange
        string jsonResponse = @"
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

        Mock<HttpMessageHandler> handlerMock = new(MockBehavior.Strict);
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

        HttpClient httpClient = new(handlerMock.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(httpClient);

        WeatherPublisherService service = new(
            _mockOptions.Object,
            _mockPublisher.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        await service.PublishWeatherUpdateAsync(_mockOptions.Object.CurrentValue, CancellationToken.None);

        // Assert
        _mockPublisher.Verify(p => p.PublishAsync(
            "Wetter",
            "Info",
            "Aktuelles Wetter in TestCity: 22,5°C, Leichter Regen.",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // [E]RROR: Invalid JSON structure lacking required fields
    [Fact]
    public async Task PublishWeatherUpdate_WhenJsonIsMissingTemp_LogsErrorAndDoesNotPublish()
    {
        // Arrange
        string jsonResponse = @"{ ""main"": { ""humidity"": 50 } }"; // Missing temp

        Mock<HttpMessageHandler> handlerMock = new(MockBehavior.Strict);
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

        HttpClient httpClient = new(handlerMock.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(httpClient);

        WeatherPublisherService service = new(
            _mockOptions.Object,
            _mockPublisher.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        // Act
        await service.PublishWeatherUpdateAsync(_mockOptions.Object.CurrentValue, CancellationToken.None);

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

        // [R]IGHT: Background service correctly exits loop upon cancellation
    [Fact]
    public async Task ExecuteAsync_BackgroundLoop_RespectsCancellation()
    {
        // Arrange
        // Loose mock behavior since we don't care what the background task exactly fetches before being cancelled
            Mock<HttpMessageHandler> handlerMock = new();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
                
            _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(new HttpClient(handlerMock.Object));

            WeatherPublisherService service = new(
                _mockOptions.Object,
                _mockPublisher.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            // Act
            CancellationTokenSource cts = new();
            await service.StartAsync(cts.Token);
            
            // Cancel to break the background loop
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(service.ExecuteTask?.IsCompleted);
        }
    }
