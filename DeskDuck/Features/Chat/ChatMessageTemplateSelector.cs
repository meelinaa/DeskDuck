using DeskDuck.Core.Features.Chat;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskDuck.Features.Chat;

/// <summary>
/// Selects between two DataTemplates based on the sender of a <see cref="ChatMessage"/>.
/// User messages use the right-aligned blue template; AI messages use the left-aligned gray template.
/// By keeping this logic in the UI project, the Core model stays free of any WinUI dependencies.
/// </summary>
public class ChatMessageTemplateSelector : DataTemplateSelector
{
    /// <summary>Gets or sets the DataTemplate used to render messages sent by the user (right-aligned, blue bubble).</summary>
    public DataTemplate? UserTemplate { get; set; }

    /// <summary>Gets or sets the DataTemplate used to render messages sent by the AI assistant (left-aligned, gray bubble).</summary>
    public DataTemplate? AssistantTemplate { get; set; }

    /// <summary>
    /// Returns <see cref="UserTemplate"/> when the item is a user message, otherwise <see cref="AssistantTemplate"/>.
    /// Falls back to the base implementation if the item is not a <see cref="ChatMessage"/> or a template is missing.
    /// </summary>
    /// <param name="item">The data item for which a template is being selected.</param>
    /// <param name="container">The container element that will display the item.</param>
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is ChatMessage message)
        {
            return message.IsUser ? UserTemplate : AssistantTemplate;
        }

        return base.SelectTemplateCore(item, container);
    }
}
