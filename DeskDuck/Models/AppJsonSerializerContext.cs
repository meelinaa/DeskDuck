using System.Text.Json.Serialization;
using DeskDuck.Models;

namespace DeskDuck
{
    /// <summary>
    /// Source-generated <see cref="JsonSerializerContext"/> that provides AOT-safe
    /// serialization metadata for all configuration model types. Registered via
    /// <see cref="System.Text.Json.JsonSerializerOptions.TypeInfoResolver"/> wherever
    /// NativeAOT-compatible deserialization is needed.
    /// </summary>
    [JsonSerializable(typeof(AppSettingsModel))]
    [JsonSerializable(typeof(PublishersSection))]
    [JsonSerializable(typeof(GeneralSection))]
    [JsonSerializable(typeof(SystemMonitorOptions))]
    [JsonSerializable(typeof(WeatherPublisherOptions))]
    [JsonSerializable(typeof(RabbitMqOptions))]
    [JsonSerializable(typeof(OllamaOptions))]
    [JsonSerializable(typeof(DuckConfig))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }
}
