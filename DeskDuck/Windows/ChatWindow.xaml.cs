using DeskDuck.ViewModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.System;

namespace DeskDuck
{
    /// <summary>
    /// Chat window that lets the user converse with the locally running Ollama AI model.
    /// The window is always on top and has its title-bar icon removed for a cleaner look.
    /// </summary>
    public sealed partial class ChatWindow : Window
    {
        /// <summary>The view model that drives the chat UI.</summary>
        public ChatViewModel ChatViewModel { get; }

        /// <summary>
        /// Initializes the chat window: sets the title, fixes the size, keeps it always on top,
        /// and clears the window icon via Win32 messages so the title bar is uncluttered.
        /// Model loading is deferred to the first Activated event to avoid blocking the constructor.
        /// </summary>
        public ChatWindow(ChatViewModel chatViewModel)
        {
            InitializeComponent();

            ChatViewModel = chatViewModel;

            Title = "Chat mit DeskDuck AI";
            AppWindow.Resize(new SizeInt32(400, 550));

            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            var presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }

            Activated += ChatWindow_Activated;
        }

        private bool _isLoaded = false;

        /// <summary>
        /// Loads the available Ollama models the first time the window is activated.
        /// The guard flag prevents redundant loads on subsequent activations.
        /// </summary>
        private async void ChatWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (!_isLoaded)
            {
                _isLoaded = true;
                await ChatViewModel.LoadModelsAsync();
            }
        }

        /// <summary>Handles the Send button click by delegating to <see cref="SendCurrentMessage"/>.</summary>
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendCurrentMessage();
        }

        /// <summary>
        /// Intercepts the Enter key in the input box to submit the message without
        /// inserting a newline, matching standard chat application behaviour.
        /// </summary>
        private async void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await SendCurrentMessage();
            }
        }

        /// <summary>
        /// Sends the current message: scrolls to the bottom immediately so the user
        /// message is visible while waiting, then awaits the AI response and scrolls again.
        /// Returns early if the input box is empty.
        /// </summary>
        private async Task SendCurrentMessage()
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
                return;
            var sendTask = ChatViewModel.SendMessageAsync();
            ScrollToBottom();
            await sendTask;
            ScrollToBottom();
            InputTextBox.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Scrolls the message list to the most recently added item so the latest
        /// message is always visible without the user having to scroll manually.
        /// </summary>
        private void ScrollToBottom()
        {
            if (ChatViewModel.Messages.Count > 0)
            {
                MessagesList.ScrollIntoView(ChatViewModel.Messages[ChatViewModel.Messages.Count - 1]);
            }
        }
    }
}
