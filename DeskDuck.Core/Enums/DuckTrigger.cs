namespace DeskDuck.Enums;

/// <summary>
/// Triggers used by the DuckStateMachine to transition between states.
/// </summary>
public enum DuckTrigger
{
    StartWalkingLeft,
    StartWalkingRight,
    ReachDestination,
    Hold,
    Release,
    Stop,
    Resume
}
