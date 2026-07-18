using DeskDuck.Core.Enums;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;

namespace DeskDuck.Core.Manager;

/// <summary>
/// Manages the autonomous movement and state transitions of the duck overlay window.
/// </summary>
public interface IDuckMovementManager
{
    /// <summary>
    /// Event triggered when the duck changes its animation or behavioral state.
    /// </summary>
    event Action<DuckState>? StateChanged;

    /// <summary>
    /// Event triggered when the duck's screen position changes.
    /// </summary>
    event Action<int, int>? PositionChanged;

    /// <summary>
    /// Initializes the movement manager with the main application window and a dispatcher queue for UI updates.
    /// </summary>
    /// <param name="appWindow">The application window representing the duck.</param>
    /// <param name="dispatcherQueue">The dispatcher queue to synchronize state changes with the UI thread.</param>
    void Initialize(AppWindow appWindow, DispatcherQueue dispatcherQueue);

    /// <summary>
    /// Temporarily pauses the duck's movement logic (e.g., when a chat window is active).
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes the duck's autonomous movement logic after it was paused.
    /// </summary>
    void Resume();

    /// <summary>
    /// Completely stops the duck's movement logic and terminates its background tasks.
    /// </summary>
    void Stop();

    /// <summary>
    /// Starts the duck's autonomous movement logic and background loop.
    /// </summary>
    void Start();

    /// <summary>
    /// Teleports the duck instantly to the specified screen coordinates.
    /// </summary>
    /// <param name="x">The new X coordinate on the screen.</param>
    /// <param name="y">The new Y coordinate on the screen.</param>
    void TeleportTo(double x, double y);
}
