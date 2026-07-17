using DeskDuck.Models;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeskDuck.Services
{
    /// <summary>
    /// Wraps the OllamaSharp client to provide AI chat functionality backed by a locally
    /// running Ollama instance. Model name, server URL, and system prompt are loaded from
    /// config.json at startup and fall back to hardcoded defaults if the file is absent.
    /// </summary>
    public class OllamaChatService
    {
        private string _modelName = "llama3.2:latest";
        private string _ollamaUrl = "http://localhost:11434";
        private string _modelPromt = string.Empty;
        private OllamaApiClient? _client;

        /// <summary>
        /// Loads configuration from disk and initializes the Ollama API client.
        /// </summary>
        public OllamaChatService()
        {
            LoadConfig();
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
        /// Reads Ollama configuration from the central appsettings.json.
        /// Silently falls back to defaults if the file does not exist or cannot be parsed.
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                var settings = Helper.ConfigHelper.LoadSettings();
                if (settings?.Ollama != null)
                {
                    if (!string.IsNullOrEmpty(settings.Ollama.Model))
                    {
                        _modelName = settings.Ollama.Model;
                    }
                    if (!string.IsNullOrEmpty(settings.Ollama.Url))
                    {
                        _ollamaUrl = settings.Ollama.Url;
                    }
                    if (!string.IsNullOrEmpty(settings.Ollama.Prompt))
                    {
                        _modelPromt = settings.Ollama.Prompt;
                    }
                }
            }
            catch
            {
                // Fall back to default field initializers.
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
