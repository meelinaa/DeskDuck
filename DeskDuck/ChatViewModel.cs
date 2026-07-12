using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DeskDuck
{
    public class ChatMessage
    {
        public string Text { get; set; } = string.Empty;
        public bool IsUser { get; set; }

        public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        
        // Push user bubble to the right, AI bubble to the left
        public Thickness BubbleMargin => IsUser ? new Thickness(60, 4, 12, 4) : new Thickness(12, 4, 60, 4);

        public Brush BackgroundBrush => IsUser 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212)) // Microsoft Blue Accent
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)); // Modern light gray

        public Brush ForegroundBrush => IsUser 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
    }

    public class ChatViewModel : INotifyPropertyChanged
    {
        private string _inputText = string.Empty;
        private bool _isTyping;

        private string _selectedModel = string.Empty;

        public ObservableCollection<ChatMessage> Messages { get; } = new();
        public ObservableCollection<string> Models { get; } = new();

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
            var modelsList = await _aiService.GetLocalModelsAsync();
            var list = modelsList.ToList();

            Models.Clear();
            if (list.Count > 0)
            {
                foreach (var m in list)
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
