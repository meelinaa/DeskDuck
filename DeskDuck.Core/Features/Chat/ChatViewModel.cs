using CommunityToolkit.Mvvm.Messaging;
using DeskDuck.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DeskDuck.ViewModel;

namespace DeskDuck.Features.Chat
{
    /// <summary>
    /// View model for the chat window. Maintains the message history, the list of
    /// available Ollama models, the currently selected model, and the typing indicator.
    /// Delegates AI requests to <see cref="OllamaChatService"/>.
    /// </summary>
    public partial class ChatViewModel : ViewModelBase
    {
        private string _inputText = string.Empty;
        private bool _isTyping;

        private string _selectedModel = string.Empty;

        /// <summary>The ordered list of chat messages displayed in the UI.</summary>
        public ObservableCollection<ChatMessage> Messages { get; } = [];

        /// <summary>The list of locally available Ollama model names for the model picker.</summary>
        public ObservableCollection<string> Models { get; } = [];

        /// <summary>
        /// The Ollama model currently selected by the user.
        /// Changes are propagated to the UI via <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }

        /// <summary>
        /// The text currently typed by the user in the input box.
        /// Bound two-way so the view model can clear it after a message is sent.
        /// </summary>
        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        /// <summary>
        /// Indicates whether the AI is currently generating a response.
        /// Also triggers an update of <see cref="TypingIndicatorVisibility"/> so the
        /// animated typing indicator in the UI is shown or hidden automatically.
        /// </summary>
        public bool IsTyping
        {
            get => _isTyping;
            set
            {
                if (SetProperty(ref _isTyping, value))
                {
                    OnPropertyChanged(nameof(TypingIndicatorVisibility));
                }
            }
        }

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
}
