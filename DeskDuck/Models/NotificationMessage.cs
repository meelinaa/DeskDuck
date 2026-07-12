namespace DeskDuck.Models;

public class NotificationMessage
{
    private string? _title;
    private string? _message;

    public string? Source { get; set; }
    public string? Severity { get; set; }
    public string? Text { get; set; }
    public string? Link { get; set; }

    public string? Title
    {
        get => _title ?? (string.IsNullOrEmpty(Source) ? "Notification" : Source);
        set => _title = value;
    }

    public string Message
    {
        get => _message ?? Text ?? string.Empty;
        set => _message = value;
    }
}
