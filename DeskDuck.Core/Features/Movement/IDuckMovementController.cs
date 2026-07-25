using DeskDuck.Core.Enums;

namespace DeskDuck.Core.Features.Movement;

/// <summary>
/// Manages the autonomous movement and state transitions of the duck overlay window.
/// This interface is entirely free of WinUI dependencies — callers supply screen and
/// duck dimensions, and the manager communicates position updates through events.
/// </summary>
public interface IDuckMovementController
{
    /// <summary>
    /// Event triggered when the duck changes its animation or behavioral state.
    /// </summary>
    event Action<DuckState>? StateChanged;

    /// <summary>
    /// Event triggered when the duck's calculated screen position changes.
    /// Subscribers (typically the UI code-behind) are responsible for physically
    /// moving the window to the reported coordinates.
    /// </summary>
    event Action<int, int>? PositionChanged;

    /// <summary>
    /// Initializes the movement manager with the logical bounds of the screen and the duck.
    /// Must be called before <see cref="Start"/>. Can be called again to resize the play area
    /// (e.g. after a display resolution change).
    /// </summary>
    /// <param name="screenWidth">Total width of the primary screen in device pixels.</param>
    /// <param name="screenHeight">Total height of the primary screen in device pixels.</param>
    /// <param name="duckWidth">Width of the duck window in device pixels.</param>
    /// <param name="duckHeight">Height of the duck window in device pixels.</param>
    void Initialize(int screenWidth, int screenHeight, int duckWidth, int duckHeight);

    /// <summary>
    /// Temporarily pauses the duck's movement logic (e.g., when the user is dragging it).
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes the duck's autonomous movement logic after it was paused.
    /// </summary>
    /// <param name="currentX">The current X position of the duck window, so the manager can sync its internal state.</param>
    /// <param name="currentY">The current Y position of the duck window, so the manager can sync its internal state.</param>
    void Resume(double currentX, double currentY);

    /// <summary>
    /// Completely stops the duck's movement logic and terminates its background timer.
    /// </summary>
    void Stop();

    /// <summary>
    /// Starts the duck's autonomous movement logic.
    /// </summary>
    /// <param name="currentX">The initial X position of the duck window.</param>
    /// <param name="currentY">The initial Y position of the duck window.</param>
    void Start(double currentX, double currentY);

    /// <summary>
    /// Teleports the duck instantly to the specified screen coordinates.
    /// Fires <see cref="PositionChanged"/> to notify the UI of the new position.
    /// </summary>
    /// <param name="x">The new X coordinate on the screen.</param>
    /// <param name="y">The new Y coordinate on the screen.</param>
    void TeleportTo(double x, double y);
}
