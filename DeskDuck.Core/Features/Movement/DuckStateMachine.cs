using DeskDuck.Core.Enums;
using Stateless;

namespace DeskDuck.Core.Features.Movement;

/// <summary>
/// Encapsulates the Stateless state machine for the duck's behavioral states.
/// </summary>
public class DuckStateMachine
{
    private readonly StateMachine<DuckState, DuckTrigger> _machine;

    /// <summary>
    /// Raised whenever the state machine transitions from one state to another.
    /// </summary>
    public event Action<DuckState>? OnStateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckStateMachine"/> class and configures state transitions.
    /// </summary>
    public DuckStateMachine()
    {
        _machine = new StateMachine<DuckState, DuckTrigger>(DuckState.Waiting);

        // From Waiting
        _machine.Configure(DuckState.Waiting)
            .Permit(DuckTrigger.StartWalkingLeft, DuckState.WalkingLeft)
            .Permit(DuckTrigger.StartWalkingRight, DuckState.WalkingRight)
            .Permit(DuckTrigger.Hold, DuckState.Held)
            .Permit(DuckTrigger.Stop, DuckState.Stopped);

        // From WalkingLeft
        _machine.Configure(DuckState.WalkingLeft)
            .Permit(DuckTrigger.ReachDestination, DuckState.Waiting)
            .Permit(DuckTrigger.Hold, DuckState.Held)
            .Permit(DuckTrigger.Stop, DuckState.Stopped)
            // It can change direction mid-walk if desired, though normally it goes to Waiting first.
            .Permit(DuckTrigger.StartWalkingRight, DuckState.WalkingRight);

        // From WalkingRight
        _machine.Configure(DuckState.WalkingRight)
            .Permit(DuckTrigger.ReachDestination, DuckState.Waiting)
            .Permit(DuckTrigger.Hold, DuckState.Held)
            .Permit(DuckTrigger.Stop, DuckState.Stopped)
            .Permit(DuckTrigger.StartWalkingLeft, DuckState.WalkingLeft);

        // From Held (Paused/Drag)
        _machine.Configure(DuckState.Held)
            .Permit(DuckTrigger.Release, DuckState.Waiting)
            .Permit(DuckTrigger.Stop, DuckState.Stopped);

        // From Stopped
        _machine.Configure(DuckState.Stopped)
            .Permit(DuckTrigger.Resume, DuckState.Waiting);

        // Hook up the event
        _machine.OnTransitioned(t => OnStateChanged?.Invoke(t.Destination));
    }

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public DuckState CurrentState => _machine.State;

    /// <summary>
    /// Fires a trigger to transition the state machine.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    public void Fire(DuckTrigger trigger)
    {
        if (_machine.CanFire(trigger))
            _machine.Fire(trigger);
    }
}
