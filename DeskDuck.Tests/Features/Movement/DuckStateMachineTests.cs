using DeskDuck.Core.Enums;
using DeskDuck.Core.Features.Movement;

namespace DeskDuck.Tests.Features.Movement;

/// <summary>
/// Unit tests for <see cref="DuckStateMachine"/>.
/// No WinUI or platform dependencies – pure state logic.
/// </summary>
public class DuckStateMachineTests
{
    // [R]IGHT: New instance is in Waiting state
    [Fact]
    public void InitialState_IsWaiting()
    {
        // Arrange & Act
        DuckStateMachine machine = new();

        // Assert
        Assert.Equal(DuckState.Waiting, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_StartWalkingLeft_FromWaiting_TransitionsToWalkingLeft()
    {
        // Arrange
        DuckStateMachine machine = new();

        // Act
        machine.Fire(DuckTrigger.StartWalkingLeft);

        // Assert
        Assert.Equal(DuckState.WalkingLeft, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_StartWalkingRight_FromWaiting_TransitionsToWalkingRight()
    {
        // Arrange
        DuckStateMachine machine = new();

        // Act
        machine.Fire(DuckTrigger.StartWalkingRight);

        // Assert
        Assert.Equal(DuckState.WalkingRight, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_ReachDestination_FromWalkingLeft_TransitionsToWaiting()
    {
        // Arrange
        DuckStateMachine machine = new();
        machine.Fire(DuckTrigger.StartWalkingLeft);

        // Act
        machine.Fire(DuckTrigger.ReachDestination);

        // Assert
        Assert.Equal(DuckState.Waiting, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_ReachDestination_FromWalkingRight_TransitionsToWaiting()
    {
        // Arrange
        DuckStateMachine machine = new();
        machine.Fire(DuckTrigger.StartWalkingRight);

        // Act
        machine.Fire(DuckTrigger.ReachDestination);

        // Assert
        Assert.Equal(DuckState.Waiting, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_Hold_FromWalkingRight_TransitionsToHeld()
    {
        // Arrange
        DuckStateMachine machine = new();
        machine.Fire(DuckTrigger.StartWalkingRight);

        // Act
        machine.Fire(DuckTrigger.Hold);

        // Assert
        Assert.Equal(DuckState.Held, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_Release_FromHeld_TransitionsToWaiting()
    {
        // Arrange
        DuckStateMachine machine = new();
        machine.Fire(DuckTrigger.Hold);

        // Act
        machine.Fire(DuckTrigger.Release);

        // Assert
        Assert.Equal(DuckState.Waiting, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_Stop_FromWaiting_TransitionsToStopped()
    {
        // Arrange
        DuckStateMachine machine = new();

        // Act
        machine.Fire(DuckTrigger.Stop);

        // Assert
        Assert.Equal(DuckState.Stopped, machine.CurrentState);
    }

    // [R]IGHT: Transitions correctly
    [Fact]
    public void Fire_Resume_FromStopped_TransitionsToWaiting()
    {
        // Arrange
        DuckStateMachine machine = new();
        machine.Fire(DuckTrigger.Stop);

        // Act
        machine.Fire(DuckTrigger.Resume);

        // Assert
        Assert.Equal(DuckState.Waiting, machine.CurrentState);
    }

    /// <summary>
    /// Tests that the state machine allows changing direction directly from WalkingLeft to WalkingRight.
    /// This ensures mid-walk direction changes don't require an intermediate Waiting state.
    // [B]OUNDARY: Mid-walk direction change
    [Fact]
    public void Fire_StartWalkingRight_FromWalkingLeft_TransitionsToWalkingRight()
    {
        // Arrange
        DuckStateMachine machine = new();
        machine.Fire(DuckTrigger.StartWalkingLeft);

        // Act
        machine.Fire(DuckTrigger.StartWalkingRight);

        // Assert
        Assert.Equal(DuckState.WalkingRight, machine.CurrentState);
    }

    /// <summary>
    /// Tests that the state machine allows changing direction directly from WalkingRight to WalkingLeft.
    /// This ensures mid-walk direction changes don't require an intermediate Waiting state.
    // [B]OUNDARY: Mid-walk direction change
    [Fact]
    public void Fire_StartWalkingLeft_FromWalkingRight_TransitionsToWalkingLeft()
    {
        // Arrange
        DuckStateMachine machine = new();
        machine.Fire(DuckTrigger.StartWalkingRight);

        // Act
        machine.Fire(DuckTrigger.StartWalkingLeft);

        // Assert
        Assert.Equal(DuckState.WalkingLeft, machine.CurrentState);
    }

    // [B]OUNDARY: Invalid trigger for current state
    [Fact]
    public void Fire_InvalidTrigger_DoesNotThrow_AndStateUnchanged()
    {
        // Arrange
        DuckStateMachine machine = new();
        // ReachDestination has no valid transition from Waiting
        DuckState stateBefore = machine.CurrentState;

        // Act – must not throw
        Exception ex = Record.Exception(() => machine.Fire(DuckTrigger.ReachDestination));

        // Assert
        Assert.Null(ex);
        Assert.Equal(stateBefore, machine.CurrentState);
    }

    // [R]IGHT: Event is raised
    [Fact]
    public void OnStateChanged_IsFired_WhenTransitionOccurs()
    {
        // Arrange
        DuckStateMachine machine = new();
        DuckState? reportedState = null;
        machine.OnStateChanged += state => reportedState = state;

        // Act
        machine.Fire(DuckTrigger.StartWalkingLeft);

        // Assert
        Assert.Equal(DuckState.WalkingLeft, reportedState);
    }
}
