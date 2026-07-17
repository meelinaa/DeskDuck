namespace DeskDuck.Models
{
    /// <summary>
    /// Configuration options for the RabbitMQ broker connection.
    /// Maps to the "RabbitMQ" section of appsettings.json.
    /// </summary>
    public class RabbitMqOptions
    {
        /// <summary>The hostname of the RabbitMQ server.</summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>The username to authenticate with RabbitMQ.</summary>
        public string UserName { get; set; } = "deskduck";

        /// <summary>The password to authenticate with RabbitMQ.</summary>
        public string Password { get; set; } = "deskduck";

        /// <summary>The name of the RabbitMQ queue to publish notifications to.</summary>
        public string QueueName { get; set; } = "deskduck.notifications";
    }
}
