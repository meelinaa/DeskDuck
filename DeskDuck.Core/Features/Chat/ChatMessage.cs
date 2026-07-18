using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DeskDuck.Core.Features.Chat;

/// <summary>
/// Represents a single message in the duck chat conversation.
/// Provides computed layout properties so the XAML data template can position and
/// style user messages (right-aligned, blue) and AI messages (left-aligned, gray)
/// without requiring a converter or code-behind logic.
/// </summary>
public class ChatMessage
{
    /// <summary>The text content of this message.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary><c>true</c> if this message was sent by the user; <c>false</c> for AI responses.</summary>
    public bool IsUser { get; set; }

    /// <summary>Aligns user bubbles to the right and AI bubbles to the left.</summary>
    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    /// <summary>
    /// Provides an asymmetric margin so the opposite side of each bubble has extra space,
    /// preventing bubbles from spanning the full width of the list.
    /// </summary>
    public Thickness BubbleMargin => IsUser ? new Thickness(60, 4, 12, 4) : new Thickness(12, 4, 60, 4);

    /// <summary>Microsoft blue accent for user messages; light gray for AI messages.</summary>
    public Brush BackgroundBrush => IsUser
        ? new SolidColorBrush(Color.FromArgb(255, 0, 120, 212))
        : new SolidColorBrush(Color.FromArgb(255, 240, 240, 240));

    /// <summary>White text on the blue user bubble; black text on the gray AI bubble.</summary>
    public Brush ForegroundBrush => IsUser
        ? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
        : new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
}
