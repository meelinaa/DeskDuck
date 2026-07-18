namespace DeskDuck.Core.Enums;

/// <summary>
/// Represents the current animation/behavioral state of the duck overlay.
/// Used to select the correct GIF asset and to control movement logic.
/// </summary>
public enum DuckState
{
    /// <summary>
    /// The duck is moving towards the left side of the screen.
    /// </summary>
    WalkingLeft,

    /// <summary>
    /// The duck is moving towards the right side of the screen.
    /// </summary>
    WalkingRight,

    /// <summary>
    /// The duck is currently sitting idly and not moving.
    /// </summary>
    Waiting,

    /// <summary>
    /// The duck is being dragged or held by the user.
    /// </summary>
    Held,

    /// <summary>
    /// The duck has stopped moving, typically prior to changing direction or state.
    /// </summary>
    Stopped,
}
