using DeskDuck.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeskDuck.Features.Chat
{
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
        /// Sends the full conversation history to the model and returns the assistant's reply.
        /// </summary>
        /// <param name="history">The ordered chat history including the latest user message.</param>
        /// <param name="modelName">
        /// Optional model override for this call. Uses the configured default when empty.
        /// </param>
        Task<string> AskAsync(IEnumerable<ChatMessage> history, string modelName);
    }
}
