namespace DeskDuck.Core.Features.Shell;

/// <summary>
/// Manages the lifecycle and state of auxiliary windows (e.g., chat, settings) associated with the duck.
/// This interface is UI-framework-agnostic and lives in the Core layer. Concrete implementations in
/// the UI project may require additional setup (e.g., passing an AppWindow reference) via their own
/// constructor or a separate initializer, but those platform-specific details do not leak into this contract.
/// </summary>
public interface IDuckWindowManager
{
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

    /// <summary>
    /// Closes all auxiliary windows and requests the application to shut down.
    /// Calling this instead of <see cref="CloseAll"/> ensures a clean exit.
    /// </summary>
    void Shutdown();
}
