using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskDuck.Core.Features.Chat;

/// <summary>
/// Represents a single message in the duck chat conversation.
/// Contains only data — visual presentation (alignment, colors, margins)
/// is handled exclusively in the UI layer via DataTemplateSelector and DataTemplates.
/// </summary>
public partial class ChatMessage : ObservableObject
{
    /// <summary>Gets or sets the text content of this message.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this message was sent by the user.
    /// When <c>false</c>, the message originates from the AI assistant.
    /// </summary>
    public bool IsUser { get; set; }
}
