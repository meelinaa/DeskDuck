namespace DeskDuck.Core.Models;

/// <summary>
/// Data transfer object for notification messages exchanged via RabbitMQ.
/// Provides a computed <see cref="Title"/> that falls back to the source name when no
/// explicit title is set, and a computed <see cref="Message"/> that falls back to
/// <see cref="Text"/> when no explicit message body is set.
/// </summary>
public class NotificationMessage
{
    private string? _title;
    private string? _message;

    /// <summary>The originating service or feature (e.g. "SystemMonitor", "Wetter").</summary>
    public string? Source { get; set; }

    /// <summary>Severity level of the notification (e.g. "Warning", "Info").</summary>
    public string? Severity { get; set; }

    /// <summary>The raw notification text as published by the producer.</summary>
    public string? Text { get; set; }

    /// <summary>Optional hyperlink associated with the notification.</summary>
    public string? Link { get; set; }

    /// <summary>
    /// Display title for the notification bubble. Falls back to the <see cref="Source"/>
    /// value, or "Notification" if source is also absent.
    /// </summary>
    public string? Title
    {
        get => _title ?? (string.IsNullOrEmpty(Source) ? "Notification" : Source);
        set => _title = value;
    }

    /// <summary>
    /// Display body of the notification bubble. Falls back to <see cref="Text"/>
    /// so producers only need to set one of the two properties.
    /// </summary>
    public string Message
    {
        get => _message ?? Text ?? string.Empty;
        set => _message = value;
    }
}
