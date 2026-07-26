using DeskDuck.Core.Features.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace DeskDuck.Tests.Features.Chat;

/// <summary>
/// Unit tests for <see cref="OllamaChatService"/>.
/// </summary>
public class OllamaChatServiceTests
{
    private readonly Mock<IOptionsMonitor<OllamaOptions>> _mockOptions;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<OllamaChatService>> _mockLogger;

    public OllamaChatServiceTests()
    {
        _mockOptions = new Mock<IOptionsMonitor<OllamaOptions>>();
        _mockOptions.Setup(o => o.CurrentValue).Returns(new OllamaOptions { Url = "http://localhost:11434", Model = "default-model", Prompt = "test" });
        
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<OllamaChatService>>();
    }

    /// <summary>
    /// Tests that an exception during HttpClient creation in InitClient is swallowed,
    /// and a subsequent call to AskStreamAsync returns the graceful fallback message.
    /// Covers Error Conditions.
    // [E]RROR: Exception thrown during InitClient
    [Fact]
    public async Task InitClient_Exception_IsSwallowed_And_AskStreamAsync_YieldsFallbackError()
    {
        // Arrange
        // Force an exception during initialization
        _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Throws(new Exception("Factory crash"));

        OllamaChatService service = new(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);
        
        // Act
        var resultStream = service.AskStreamAsync(new List<ChatMessage>(), string.Empty);
        
        var results = new List<string>();
        await foreach (var chunk in resultStream)
        {
            results.Add(chunk);
        }

        // Assert
        Assert.Single(results);
        Assert.Equal("Entschuldigung, ich konnte nicht mit meinem Gehirn (Ollama) verbinden.", results[0]);
    }

    /// <summary>
    /// Tests that the AskStreamAsync method returns a specific error message if the stream throws mid-way.
    /// Covers Error Conditions.
    // [E]RROR: Exception thrown while consuming stream
    [Fact]
    public async Task AskStreamAsync_StreamError_YieldsConnectionError()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        
        // We simulate a stream that throws halfway through reading (or immediately upon sending).
        // For simplicity, we just throw on SendAsync. OllamaSharp's ChatAsync will throw, which is caught inside the enumerator loop or initial setup.
        // Wait, if it throws on SendAsync, it will be caught by the outer catch in AskStreamAsync before enumerating!
        // To test the INNER catch block (during MoveNextAsync), we need SendAsync to return a success response with a stream that throws when read.
        
        var faultyStream = new FaultyStream();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(faultyStream)
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:11434") };
        _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(httpClient);

        OllamaChatService service = new(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var resultStream = service.AskStreamAsync(new List<ChatMessage>(), string.Empty);
        
        var results = new List<string>();
        await foreach (var chunk in resultStream)
        {
            results.Add(chunk);
        }

        // Assert
        // The first yield might be caught by the MoveNextAsync block or earlier, but we expect the specific stream error or init error.
        // Actually, if it throws during reading the stream, it yields "\n[Fehler bei der Verbindung]"
        Assert.Contains("\n[Fehler bei der Verbindung]", results);
    }

    /// <summary>
    /// Tests that the provided modelName overrides the configured default model.
    /// Covers Branch Coverage (Ternary operators in AskStreamAsync).
    // [R]IGHT: Specific modelName provided takes precedence over config
    [Fact]
    public async Task AskStreamAsync_ModelName_OverridesConfig()
    {
        // Arrange
        string capturedContent = string.Empty;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedContent = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:11434") };
        _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(httpClient);

        OllamaChatService service = new(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var resultStream = service.AskStreamAsync(new List<ChatMessage>(), "override-model");
        
        // Just move next once to trigger the request
        await using var enumerator = resultStream.GetAsyncEnumerator();
        await enumerator.MoveNextAsync();

        // Assert
        Assert.NotEmpty(capturedContent);
        
        // Assert the model in the JSON payload is "override-model"
        Assert.Contains("\"model\":\"override-model\"", capturedContent);
    }

    /// <summary>
    /// Tests the fallback response when the model returns no content and no error occurs.
    /// Covers Boundary.
    // [B]OUNDARY: API returns success but stream contains zero messages
    [Fact]
    public async Task AskStreamAsync_NoContent_YieldsNoAnswerFallback()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        
        // Return a response that OllamaSharp parses as "done" but with no message content chunks.
        // Typically Ollama yields JSON lines. We return an empty stream or a stream with no message content.
        string ndjson = "{\"model\":\"override-model\",\"created_at\":\"2024-01-01T00:00:00Z\",\"done\":true}\n";

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ndjson) }); 

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:11434") };
        _mockHttpClientFactory.Setup(f => f.CreateClient("DeskDuck")).Returns(httpClient);

        OllamaChatService service = new(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var resultStream = service.AskStreamAsync(new List<ChatMessage>(), string.Empty);
        
        var results = new List<string>();
        await foreach (var chunk in resultStream)
        {
            results.Add(chunk);
        }

        // Assert
        Assert.Single(results);
        Assert.Equal("Quack... Ich habe keine Antwort erhalten.", results[0]);
    }

    /// <summary>
    /// Stream that throws an Exception on Read to simulate connection loss mid-stream.
    /// </summary>
    private class FaultyStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Network dropped");
        
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new IOException("Network dropped");
        }
        
#if NETCOREAPP3_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("Network dropped");
        }
#endif

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
