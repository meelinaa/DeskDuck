using DeskDuck.Features.Chat;
using Moq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DeskDuck.Tests.Features.Chat
{
    /// <summary>
    /// Unit tests for <see cref="ChatViewModel"/>.
    /// Uses a mocked <see cref="IOllamaChatService"/> so no real Ollama instance is needed.
    /// </summary>
    public class ChatViewModelTests
    {
        private readonly Mock<IOllamaChatService> _mockAiService;

        public ChatViewModelTests()
        {
            _mockAiService = new Mock<IOllamaChatService>();
        }

        [Fact]
        public void Constructor_AddsInitialGreetingMessage()
        {
            // Arrange & Act
            var vm = new ChatViewModel(_mockAiService.Object);

            // Assert
            Assert.Single(vm.Messages);
            Assert.False(vm.Messages[0].IsUser);
            Assert.Contains("Quack", vm.Messages[0].Text);
        }

        [Fact]
        public void Constructor_AddsPlaceholderModel()
        {
            // Arrange & Act
            var vm = new ChatViewModel(_mockAiService.Object);

            // Assert
            Assert.Single(vm.Models);
            Assert.Contains("Lade", vm.SelectedModel);
        }

        [Fact]
        public async Task SendMessageAsync_WithEmptyInput_DoesNothing()
        {
            // Arrange
            var vm = new ChatViewModel(_mockAiService.Object);
            vm.InputText = "   ";
            int messageCountBefore = vm.Messages.Count;

            // Act
            await vm.SendMessageAsync();

            // Assert
            Assert.Equal(messageCountBefore, vm.Messages.Count);
            _mockAiService.Verify(s => s.AskAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendMessageAsync_WithValidInput_AddsUserAndAiMessages()
        {
            // Arrange
            _mockAiService
                .Setup(s => s.AskAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<string>()))
                .ReturnsAsync("Quack! Ich bin die KI-Antwort.");

            var vm = new ChatViewModel(_mockAiService.Object);
            vm.InputText = "Hallo Ente!";

            // Act
            await vm.SendMessageAsync();

            // Assert – 1 Greeting + 1 User + 1 AI = 3
            Assert.Equal(3, vm.Messages.Count);
            Assert.True(vm.Messages[1].IsUser);
            Assert.Equal("Hallo Ente!", vm.Messages[1].Text);
            Assert.False(vm.Messages[2].IsUser);
            Assert.Equal("Quack! Ich bin die KI-Antwort.", vm.Messages[2].Text);
        }

        [Fact]
        public async Task SendMessageAsync_ClearsInputText_AfterSend()
        {
            // Arrange
            _mockAiService
                .Setup(s => s.AskAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<string>()))
                .ReturnsAsync("Antwort");

            var vm = new ChatViewModel(_mockAiService.Object);
            vm.InputText = "Eine Nachricht";

            // Act
            await vm.SendMessageAsync();

            // Assert
            Assert.Equal(string.Empty, vm.InputText);
        }

        [Fact]
        public async Task SendMessageAsync_SetsIsTypingTrue_DuringAiCall_ThenFalse()
        {
            // Arrange
            _mockAiService
                .Setup(s => s.AskAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Yield();
                    return "Antwort";
                });

            var vm = new ChatViewModel(_mockAiService.Object);

            // Track IsTyping state changes
            var typingStates = new List<bool>();
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.IsTyping))
                    typingStates.Add(vm.IsTyping);
            };

            vm.InputText = "Test";

            // Act
            await vm.SendMessageAsync();

            // Assert – IsTyping went true then false
            Assert.Contains(true, typingStates);
            Assert.False(vm.IsTyping);
        }

        [Fact]
        public async Task LoadModelsAsync_WithAvailableModels_PopulatesModels()
        {
            // Arrange
            _mockAiService
                .Setup(s => s.GetLocalModelsAsync())
                .ReturnsAsync(new[] { "llama3.2:latest", "mistral:latest" });

            var vm = new ChatViewModel(_mockAiService.Object);

            // Act
            await vm.LoadModelsAsync();

            // Assert
            Assert.Equal(2, vm.Models.Count);
            Assert.Contains("llama3.2:latest", vm.Models);
            // Pre-selects llama3.2:latest when available
            Assert.Equal("llama3.2:latest", vm.SelectedModel);
        }

        [Fact]
        public async Task LoadModelsAsync_WithNoModels_ShowsFallbackMessage()
        {
            // Arrange
            _mockAiService
                .Setup(s => s.GetLocalModelsAsync())
                .ReturnsAsync(new List<string>());

            var vm = new ChatViewModel(_mockAiService.Object);

            // Act
            await vm.LoadModelsAsync();

            // Assert
            Assert.Single(vm.Models);
            Assert.Contains("Keine Modelle", vm.SelectedModel);
        }
    }
}
