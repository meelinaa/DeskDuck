using System.ComponentModel.DataAnnotations;

namespace DeskDuck.Core.Features.Messaging;

/// <summary>
/// Configuration options for the RabbitMQ broker connection.
/// Maps to the "RabbitMQ" section of appsettings.json.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>The hostname of the RabbitMQ server.</summary>
    [Required(AllowEmptyStrings = false)]
    public string HostName { get; set; } = MessagingConstants.DefaultHostName;

    /// <summary>The port of the RabbitMQ server. Defaults to 5672.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>The username to authenticate with RabbitMQ.</summary>
    [Required(AllowEmptyStrings = false)]
    public string UserName { get; set; } = MessagingConstants.DefaultUserName;

    /// <summary>The password to authenticate with RabbitMQ.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;

    /// <summary>The name of the RabbitMQ queue to publish notifications to.</summary>
    public string QueueName { get; set; } = MessagingConstants.DefaultQueueName;
}
