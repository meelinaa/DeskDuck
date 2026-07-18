namespace DeskDuck.Core.Enums;

/// <summary>
/// Triggers used by the DuckStateMachine to transition between states.
/// </summary>
public enum DuckTrigger
{
    /// <summary>
    /// Commands the duck to start moving towards the left side of the screen.
    /// </summary>
    StartWalkingLeft,

    /// <summary>
    /// Commands the duck to start moving towards the right side of the screen.
    /// </summary>
    StartWalkingRight,

    /// <summary>
    /// Triggered when the duck reaches its intended target destination on the screen.
    /// </summary>
    ReachDestination,

    /// <summary>
    /// Triggered when the user clicks and holds the duck.
    /// </summary>
    Hold,

    /// <summary>
    /// Triggered when the user releases the duck after holding it.
    /// </summary>
    Release,

    /// <summary>
    /// Commands the duck to halt its current movement or action immediately.
    /// </summary>
    Stop,

    /// <summary>
    /// Commands the duck to resume its previous activity after being stopped or held.
    /// </summary>
    Resume
}
