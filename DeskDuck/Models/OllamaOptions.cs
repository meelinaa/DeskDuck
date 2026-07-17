namespace DeskDuck.Models
{
    /// <summary>
    /// Configuration options for the local Ollama AI model.
    /// Maps to the "Ollama" section of appsettings.json.
    /// </summary>
    public class OllamaOptions
    {
        /// <summary>The name of the Ollama model to use (e.g. llama3.2:latest).</summary>
        public string Model { get; set; } = "llama3.2:latest";

        /// <summary>The base URL of the local Ollama API service.</summary>
        public string Url { get; set; } = "http://localhost:11434";

        /// <summary>The system prompt defining the duck's personality.</summary>
        public string Prompt { get; set; } = string.Empty;
    }
}
