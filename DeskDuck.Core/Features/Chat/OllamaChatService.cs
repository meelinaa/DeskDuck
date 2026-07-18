using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text;

namespace DeskDuck.Core.Features.Chat;

/// <summary>
/// Wraps the OllamaSharp client to provide AI chat functionality backed by a locally
/// running Ollama instance. Configuration is injected via <see cref="IOptions{OllamaOptions}"/>.
/// </summary>
public class OllamaChatService : IOllamaChatService
{
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaChatService> _logger;
    private OllamaApiClient? _client;

    /// <summary>
    /// Initializes the service with the injected Ollama options and creates the API client.
    /// </summary>
    public OllamaChatService(
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaChatService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        InitClient();

        _optionsMonitor.OnChange(config =>
        {
            InitClient();
        });
    }

    /// <summary>
    /// Creates the <see cref="OllamaApiClient"/> instance pointed at the configured URL
    /// and pre-selects the configured model. Errors are swallowed here because the client
    /// can be re-initialized lazily on the first actual request.
    /// </summary>
    private void InitClient()
    {
        try
        {
            OllamaOptions config = _optionsMonitor.CurrentValue;
            string modelName = !string.IsNullOrEmpty(config.Model) ? config.Model : "llama3.2:latest";
            string ollamaUrl = !string.IsNullOrEmpty(config.Url) ? config.Url : "http://localhost:11434";

            HttpClient httpClient = _httpClientFactory.CreateClient("DeskDuck");
            httpClient.BaseAddress = new Uri(ollamaUrl);

            _client = new OllamaApiClient(httpClient)
            {
                SelectedModel = modelName
            };
        }
        catch (Exception ex)
        {
            // Swallow: client will be re-initialized lazily on the next request.
            _logger.LogDebug(ex, "Ollama client initialization failed. Will retry on next request.");
        }
    }


    /// <summary>
    /// Returns the names of all locally available Ollama models.
    /// Returns an empty sequence if the Ollama service is unavailable.
    /// </summary>
    public async Task<IEnumerable<string>> GetLocalModelsAsync()
    {
        try
        {
            if (_client == null) InitClient();
            if (_client == null) return [];

            IEnumerable<Model> models = await _client.ListLocalModelsAsync();
            return models.Select(m => m.Name);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Sends the full conversation history to the model and streams the assistant's reply chunk by chunk.
    /// Uses the globally configured prompt as a system message. If the modelName is
    /// non-empty it overrides the default configured model for this call only.
    /// Returns a user-friendly error string instead of throwing on failure.
    /// </summary>
    public async IAsyncEnumerable<string> AskStreamAsync(IEnumerable<ChatMessage> history, string modelName)
    {
        IAsyncEnumerable<ChatResponseStream?>? stream = null;
        string? errorMessage = null;

        try
        {
            if (_client == null)
            {
                InitClient();
            }

            if (_client == null)
            {
                throw new InvalidOperationException("Ollama Client could not be initialized.");
            }

            OllamaOptions config = _optionsMonitor.CurrentValue;
            string activeModel = string.IsNullOrWhiteSpace(modelName) ? (string.IsNullOrWhiteSpace(config.Model) ? "llama3.2:latest" : config.Model) : modelName;

            List<Message> messages = [];

            messages.Add(new Message(ChatRole.System, config.Prompt ?? string.Empty));

            foreach (ChatMessage chatMsg in history)
            {
                ChatRole role = chatMsg.IsUser ? ChatRole.User : ChatRole.Assistant;
                messages.Add(new Message(role, chatMsg.Text));
            }

            ChatRequest chatRequest = new()
            {
                Model = activeModel,
                Messages = messages,
                Stream = true
            };

            stream = _client.ChatAsync(chatRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama init error");
            errorMessage = "Entschuldigung, ich konnte nicht mit meinem Gehirn (Ollama) verbinden.";
        }

        if (errorMessage != null)
        {
            yield return errorMessage;
            yield break;
        }

        bool hasContent = false;
        IAsyncEnumerator<ChatResponseStream?> enumerator = stream!.GetAsyncEnumerator();

        while (true)
        {
            bool moved = false;
            try
            {
                moved = await enumerator.MoveNextAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ollama stream error");
                errorMessage = "\n[Fehler bei der Verbindung]";
            }

            if (errorMessage != null)
            {
                yield return errorMessage;
                break;
            }

            if (!moved) break;

            ChatResponseStream? response = enumerator.Current;
            if (response?.Message?.Content != null)
            {
                hasContent = true;
                yield return response.Message.Content;
            }
        }

        if (!hasContent && errorMessage == null)
        {
            yield return "Quack... Ich habe keine Antwort erhalten.";
        }
    }
}
