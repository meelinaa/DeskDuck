namespace DeskDuck.Core.Enums;

/// <summary>
/// Represents the current animation/behavioral state of the duck overlay.
/// Used to select the correct GIF asset and to control movement logic.
/// </summary>
public enum DuckState
{
    WalkingLeft,
    WalkingRight,
    Waiting,
    Held,
    Stopped
}
