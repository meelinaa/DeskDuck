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
    public class OllamaChatService
    {
        private string _modelName = "llama3.2:latest";
        private string _ollamaUrl = "http://localhost:11434";
        private string _modelPromt = string.Empty;
        private OllamaApiClient? _client;

        public OllamaChatService()
        {
            LoadConfig();
            InitClient();
        }

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
                // Client initialization will be retried or handled gracefully on call
            }
        }

        private void LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("OllamaModel", out var modelProp))
                    {
                        string? val = modelProp.GetString();
                        if (!string.IsNullOrEmpty(val))
                        {
                            _modelName = val;
                        }
                    }
                    if (root.TryGetProperty("OllamaUrl", out var urlProp))
                    {
                        string? val = urlProp.GetString();
                        if (!string.IsNullOrEmpty(val))
                        {
                            _ollamaUrl = val;
                        }
                    }
                    if (root.TryGetProperty("OllamaPromt", out var promtProp))
                    {
                        string? val = promtProp.GetString();
                        if (!string.IsNullOrEmpty(val))
                        {
                            _modelPromt = val;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default field initializers
            }
        }

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

                // 1. Build the messages list expected by OllamaSharp
                List<Message> messages = [];

                // Add System Prompt as the first message
                messages.Add(new Message(ChatRole.System, _modelPromt));

                // Add conversation history
                foreach (ChatMessage chatMsg in history)
                {
                    ChatRole role = chatMsg.IsUser ? ChatRole.User : ChatRole.Assistant;
                    messages.Add(new Message(role, chatMsg.Text));
                }

                // 2. Execute chat request
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
