using Microsoft.UI.Windowing;

namespace DeskDuck.Core.Features.Shell;

/// <summary>
/// Manages the lifecycle and state of auxiliary windows (e.g., chat, settings) associated with the duck.
/// </summary>
public interface IDuckWindowManager
{
    /// <summary>
    /// Initializes the window manager with the primary duck application window.
    /// </summary>
    /// <param name="duckAppWindow">The main application window hosting the duck overlay.</param>
    void Initialize(AppWindow duckAppWindow);

    /// <summary>
    /// Opens the chat window. If it is already open, brings it to the foreground.
    /// </summary>
    void OpenChatWindow();

    /// <summary>
    /// Opens the settings window. If it is already open, brings it to the foreground.
    /// </summary>
    void OpenSettingsWindow();

    /// <summary>
    /// Closes all auxiliary windows managed by this instance.
    /// </summary>
    void CloseAll();
}
