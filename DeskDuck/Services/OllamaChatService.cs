using DeskDuck.Models;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DeskDuck.Services
{
    /// <summary>
    /// Wraps the OllamaSharp client to provide AI chat functionality backed by a locally
    /// running Ollama instance. Configuration is injected via <see cref="IOptions{OllamaOptions}"/>.
    /// </summary>
    public class OllamaChatService : IOllamaChatService
    {
        private readonly string _modelName;
        private readonly string _ollamaUrl;
        private readonly string _modelPromt;
        private OllamaApiClient? _client;

        /// <summary>
        /// Initializes the service with the injected Ollama options and creates the API client.
        /// </summary>
        public OllamaChatService(IOptions<OllamaOptions> options)
        {
            var config = options.Value;
            _modelName = !string.IsNullOrEmpty(config.Model) ? config.Model : "llama3.2:latest";
            _ollamaUrl = !string.IsNullOrEmpty(config.Url) ? config.Url : "http://localhost:11434";
            _modelPromt = config.Prompt ?? string.Empty;
            InitClient();
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
                _client = new OllamaApiClient(new Uri(_ollamaUrl))
                {
                    SelectedModel = _modelName
                };
            }
            catch
            {
                // Client initialization will be retried lazily on the next request.
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
        /// Sends the full conversation history to Ollama and returns the assistant's reply.
        /// The configured system prompt is prepended as the first message so the model always
        /// receives its persona instructions. If <paramref name="modelName"/> is provided and
        /// non-empty it overrides the default configured model for this call only.
        /// Returns a user-friendly error string instead of throwing on failure.
        /// </summary>
        public async Task<string> AskAsync(IEnumerable<ChatMessage> history, string modelName)
        {
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

                string activeModel = string.IsNullOrWhiteSpace(modelName) ? _modelName : modelName;

                List<Message> messages = [];

                messages.Add(new Message(ChatRole.System, _modelPromt));

                foreach (ChatMessage chatMsg in history)
                {
                    ChatRole role = chatMsg.IsUser ? ChatRole.User : ChatRole.Assistant;
                    messages.Add(new Message(role, chatMsg.Text));
                }

                ChatRequest chatRequest = new()
                {
                    Model = activeModel,
                    Messages = messages,
                    Stream = false
                };

                string responseContent = string.Empty;
                await foreach (ChatResponseStream? response in _client.ChatAsync(chatRequest))
                {
                    if (response?.Message?.Content != null)
                    {
                        responseContent += response.Message.Content;
                    }
                }

                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    return "Quack... Ich habe keine Antwort erhalten.";
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ollama error: {ex}");
                return "Quack... ich finde gerade keine Verbindung zu meinem Gehirn. (Ollama-Dienst läuft nicht oder Modell ist nicht geladen)";
            }
        }
    }
}
