using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace DeskDuck
{
    public class OllamaChatService
    {
        private string _modelName = "llama3.2:latest";
        private string _ollamaUrl = "http://localhost:11434";
        private OllamaApiClient? _client;

        private const string SystemPrompt = 
            "Du bist DeskDuck, eine freundliche und hilfsbereite KI in Gestalt einer Ente, die auf dem Desktop des Nutzers lebt. " +
            "Du beantwortest alle Fragen ausführlich, korrekt und hilfreich – deine Enten-Persönlichkeit soll deine Antworten nie inhaltlich einschränken. " +
            "Gelegentlich, aber nicht in jeder Nachricht, baust du spielerisch enten-typische Ausdrücke ein (z. B. 'Quack', 'schnatter schnatter', oder kleine Anspielungen aufs Watscheln/Schwimmen), " +
            "ohne dabei albern oder unprofessionell zu wirken. Antworte in der Sprache, in der der Nutzer schreibt.";

        public OllamaChatService()
        {
            LoadConfig();
            InitClient();
        }

        private void InitClient()
        {
            try
            {
                _client = new OllamaApiClient(new Uri(_ollamaUrl));
                _client.SelectedModel = _modelName;
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
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("OllamaModel", out var modelProp))
                        {
                            var val = modelProp.GetString();
                            if (!string.IsNullOrEmpty(val))
                            {
                                _modelName = val;
                            }
                        }
                        if (root.TryGetProperty("OllamaUrl", out var urlProp))
                        {
                            var val = urlProp.GetString();
                            if (!string.IsNullOrEmpty(val))
                            {
                                _ollamaUrl = val;
                            }
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
                if (_client == null) return Enumerable.Empty<string>();

                var models = await _client.ListLocalModelsAsync();
                return models.Select(m => m.Name);
            }
            catch
            {
                return Enumerable.Empty<string>();
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
                var messages = new List<Message>();

                // Add System Prompt as the first message
                messages.Add(new Message(ChatRole.System, SystemPrompt));

                // Add conversation history
                foreach (var chatMsg in history)
                {
                    var role = chatMsg.IsUser ? ChatRole.User : ChatRole.Assistant;
                    messages.Add(new Message(role, chatMsg.Text));
                }

                // 2. Execute chat request
                var chatRequest = new ChatRequest
                {
                    Model = activeModel,
                    Messages = messages,
                    Stream = false // We fetch the full answer at once
                };

                string responseContent = string.Empty;
                await foreach (var response in _client.ChatAsync(chatRequest))
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
                System.Diagnostics.Debug.WriteLine($"Ollama error: {ex}");
                return "Quack... ich finde gerade keine Verbindung zu meinem Gehirn. (Ollama-Dienst läuft nicht oder Modell ist nicht geladen)";
            }
        }
    }
}
