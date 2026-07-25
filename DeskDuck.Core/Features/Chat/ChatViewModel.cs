using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace DeskDuck.Core.Features.Chat;

/// <summary>
/// View model for the chat window. Maintains the message history, the list of
/// available Ollama models, the currently selected model, and the typing indicator.
/// Delegates AI requests to <see cref="OllamaChatService"/>.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTyping { get; set; }

    [ObservableProperty]
    public partial string SelectedModel { get; set; }

    /// <summary>Gets the ordered list of chat messages displayed in the UI.</summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    /// <summary>Gets the list of locally available Ollama model names for the model picker.</summary>
    public ObservableCollection<string> Models { get; } = [];


    private readonly IOllamaChatService _aiService;

    /// <summary>
    /// Initializes the view model with an initial greeting message from the duck
    /// and a placeholder model entry while the real model list is loading asynchronously.
    /// </summary>
    public ChatViewModel(IOllamaChatService aiService)
    {
        _aiService = aiService;

        Messages.Add(new ChatMessage
        {
            Text = "Quack! Hallo, ich bin dein DeskDuck KI-Assistent. Wie kann ich dir heute helfen?",
            IsUser = false
        });

        Models.Add("Lade Modelle...");
        SelectedModel = Models[0];
    }

    /// <summary>
    /// Fetches the list of locally available Ollama models and populates <see cref="Models"/>.
    /// Pre-selects llama3.2:latest if available, otherwise selects the first model.
    /// Shows a user-friendly message when no models are found (e.g. Ollama is not running).
    /// </summary>
    public async Task LoadModelsAsync()
    {
        IEnumerable<string> modelsList = await _aiService.GetLocalModelsAsync();
        List<string> list = modelsList.ToList();

        Models.Clear();
        if (list.Count > 0)
        {
            foreach (string m in list)
            {
                Models.Add(m);
            }
            SelectedModel = list.Contains("llama3.2:latest") ? "llama3.2:latest" : list[0];
        }
        else
        {
            Models.Add("Keine Modelle gefunden - läuft Ollama?");
            SelectedModel = Models[0];
        }
    }

    /// <summary>
    /// Appends the user's input to the message history, clears the input box,
    /// shows the typing indicator, sends the full history to Ollama, and appends
    /// the AI response. Does nothing if the input is empty or whitespace.
    /// </summary>
    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        string userMessageText = InputText;
        InputText = string.Empty;

        // Copy current messages to pass to AI history (excluding the new empty one we're about to add)
        var historyForAi = Messages.ToList();

        Messages.Add(new ChatMessage
        {
            Text = userMessageText,
            IsUser = true
        });
        historyForAi.Add(Messages.Last());

        IsTyping = true;

        ChatMessage aiMessage = new()
        {
            Text = "",
            IsUser = false
        };
        Messages.Add(aiMessage);

        try
        {
            await foreach (var chunk in _aiService.AskStreamAsync(historyForAi, SelectedModel))
            {
                aiMessage.Text += chunk;
            }
        }
        finally
        {
            IsTyping = false;
        }
    }
}
