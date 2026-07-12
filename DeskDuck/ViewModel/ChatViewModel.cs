using DeskDuck.Models;
using DeskDuck.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DeskDuck.ViewModel
{
    public partial class ChatViewModel : INotifyPropertyChanged
    {
        private string _inputText = string.Empty;
        private bool _isTyping;

        private string _selectedModel = string.Empty;

        public ObservableCollection<ChatMessage> Messages { get; } = [];
        public ObservableCollection<string> Models { get; } = [];

        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

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

        public Visibility TypingIndicatorVisibility => IsTyping ? Visibility.Visible : Visibility.Collapsed;

        private readonly OllamaChatService _aiService = new();

        public ChatViewModel()
        {
            // Initial greeting message
            Messages.Add(new ChatMessage
            {
                Text = "Quack! Hallo, ich bin dein DeskDuck KI-Assistent. Wie kann ich dir heute helfen?",
                IsUser = false
            });

            // Add a placeholder until loaded
            Models.Add("Lade Modelle...");
            SelectedModel = Models[0];
        }

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

        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;

            string userMessageText = InputText;
            InputText = string.Empty;

            // Add user message
            Messages.Add(new ChatMessage
            {
                Text = userMessageText,
                IsUser = true
            });

            // Set typing state
            IsTyping = true;

            // Call real Ollama AI service with history and selected model
            string aiResponse = await _aiService.AskAsync(Messages, SelectedModel);

            Messages.Add(new ChatMessage
            {
                Text = aiResponse,
                IsUser = false
            });

            IsTyping = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
