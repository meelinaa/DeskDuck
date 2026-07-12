using System.Text.Json.Serialization;
using DeskDuck.Models;

namespace DeskDuck
{
    [JsonSerializable(typeof(AppSettingsModel))]
    [JsonSerializable(typeof(PublishersSection))]
    [JsonSerializable(typeof(GeneralSection))]
    [JsonSerializable(typeof(SystemMonitorOptions))]
    [JsonSerializable(typeof(WeatherPublisherOptions))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }
}
