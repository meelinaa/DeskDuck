namespace DeskDuck.Core.Features.Chat;

/// <summary>
/// Abstraction for an AI chat service backed by a locally running language model.
/// Decouples the ChatViewModel from the concrete Ollama implementation.
/// </summary>
public interface IOllamaChatService
{
    /// <summary>
    /// Returns the names of all locally available model instances.
    /// Returns an empty sequence if the service is unavailable.
    /// </summary>
    Task<IEnumerable<string>> GetLocalModelsAsync();

    /// <summary>
    /// Sends the full conversation history to the model and streams the assistant's reply chunk by chunk.
    /// </summary>
    /// <param name="history">The ordered chat history including the latest user message.</param>
    /// <param name="modelName">
    /// Optional model override for this call. Uses the configured default when empty.
    /// </param>
    IAsyncEnumerable<string> AskStreamAsync(IEnumerable<ChatMessage> history, string modelName);
}
