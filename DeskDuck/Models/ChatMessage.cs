using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DeskDuck.Models;

public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }

    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    // Push user bubble to the right, AI bubble to the left
    public Thickness BubbleMargin => IsUser ? new Thickness(60, 4, 12, 4) : new Thickness(12, 4, 60, 4);

    public Brush BackgroundBrush => IsUser
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212)) // Microsoft Blue Accent
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)); // Modern light gray

    public Brush ForegroundBrush => IsUser
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
}
