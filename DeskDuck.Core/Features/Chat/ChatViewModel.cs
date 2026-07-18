using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
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
    [NotifyPropertyChangedFor(nameof(TypingIndicatorVisibility))]
    public partial bool IsTyping { get; set; }

    [ObservableProperty]
    public partial string SelectedModel { get; set; } = string.Empty;

    /// <summary>The ordered list of chat messages displayed in the UI.</summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    /// <summary>The list of locally available Ollama model names for the model picker.</summary>
    public ObservableCollection<string> Models { get; } = [];



    /// <summary>
    /// Derived visibility value for the typing indicator, computed from <see cref="IsTyping"/>.
    /// </summary>
    public Visibility TypingIndicatorVisibility => IsTyping ? Visibility.Visible : Visibility.Collapsed;

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

        Messages.Add(new ChatMessage
        {
            Text = userMessageText,
            IsUser = true
        });

        IsTyping = true;

        string aiResponse = await _aiService.AskAsync(Messages, SelectedModel);

        Messages.Add(new ChatMessage
        {
            Text = aiResponse,
            IsUser = false
        });

        IsTyping = false;
    }

}
