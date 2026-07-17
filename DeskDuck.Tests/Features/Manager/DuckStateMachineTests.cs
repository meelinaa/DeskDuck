using DeskDuck.Enums;
using DeskDuck.Manager;

namespace DeskDuck.Tests.Features.Manager
{
    /// <summary>
    /// Unit tests for <see cref="DuckStateMachine"/>.
    /// No WinUI or platform dependencies – pure state logic.
    /// </summary>
    public class DuckStateMachineTests
    {
        [Fact]
        public void InitialState_IsWaiting()
        {
            // Arrange & Act
            var machine = new DuckStateMachine();

            // Assert
            Assert.Equal(DuckState.Waiting, machine.CurrentState);
        }

        [Fact]
        public void Fire_StartWalkingLeft_FromWaiting_TransitionsToWalkingLeft()
        {
            // Arrange
            var machine = new DuckStateMachine();

            // Act
            machine.Fire(DuckTrigger.StartWalkingLeft);

            // Assert
            Assert.Equal(DuckState.WalkingLeft, machine.CurrentState);
        }

        [Fact]
        public void Fire_StartWalkingRight_FromWaiting_TransitionsToWalkingRight()
        {
            // Arrange
            var machine = new DuckStateMachine();

            // Act
            machine.Fire(DuckTrigger.StartWalkingRight);

            // Assert
            Assert.Equal(DuckState.WalkingRight, machine.CurrentState);
        }

        [Fact]
        public void Fire_ReachDestination_FromWalkingLeft_TransitionsToWaiting()
        {
            // Arrange
            var machine = new DuckStateMachine();
            machine.Fire(DuckTrigger.StartWalkingLeft);

            // Act
            machine.Fire(DuckTrigger.ReachDestination);

            // Assert
            Assert.Equal(DuckState.Waiting, machine.CurrentState);
        }

        [Fact]
        public void Fire_ReachDestination_FromWalkingRight_TransitionsToWaiting()
        {
            // Arrange
            var machine = new DuckStateMachine();
            machine.Fire(DuckTrigger.StartWalkingRight);

            // Act
            machine.Fire(DuckTrigger.ReachDestination);

            // Assert
            Assert.Equal(DuckState.Waiting, machine.CurrentState);
        }

        [Fact]
        public void Fire_Hold_FromWalkingRight_TransitionsToHeld()
        {
            // Arrange
            var machine = new DuckStateMachine();
            machine.Fire(DuckTrigger.StartWalkingRight);

            // Act
            machine.Fire(DuckTrigger.Hold);

            // Assert
            Assert.Equal(DuckState.Held, machine.CurrentState);
        }

        [Fact]
        public void Fire_Release_FromHeld_TransitionsToWaiting()
        {
            // Arrange
            var machine = new DuckStateMachine();
            machine.Fire(DuckTrigger.Hold);

            // Act
            machine.Fire(DuckTrigger.Release);

            // Assert
            Assert.Equal(DuckState.Waiting, machine.CurrentState);
        }

        [Fact]
        public void Fire_Stop_FromWaiting_TransitionsToStopped()
        {
            // Arrange
            var machine = new DuckStateMachine();

            // Act
            machine.Fire(DuckTrigger.Stop);

            // Assert
            Assert.Equal(DuckState.Stopped, machine.CurrentState);
        }

        [Fact]
        public void Fire_Resume_FromStopped_TransitionsToWaiting()
        {
            // Arrange
            var machine = new DuckStateMachine();
            machine.Fire(DuckTrigger.Stop);

            // Act
            machine.Fire(DuckTrigger.Resume);

            // Assert
            Assert.Equal(DuckState.Waiting, machine.CurrentState);
        }

        [Fact]
        public void Fire_InvalidTrigger_DoesNotThrow_AndStateUnchanged()
        {
            // Arrange
            var machine = new DuckStateMachine();
            // ReachDestination has no valid transition from Waiting
            var stateBefore = machine.CurrentState;

            // Act – must not throw
            var ex = Record.Exception(() => machine.Fire(DuckTrigger.ReachDestination));

            // Assert
            Assert.Null(ex);
            Assert.Equal(stateBefore, machine.CurrentState);
        }

        [Fact]
        public void OnStateChanged_IsFired_WhenTransitionOccurs()
        {
            // Arrange
            var machine = new DuckStateMachine();
            DuckState? reportedState = null;
            machine.OnStateChanged += state => reportedState = state;

            // Act
            machine.Fire(DuckTrigger.StartWalkingLeft);

            // Assert
            Assert.Equal(DuckState.WalkingLeft, reportedState);
        }
    }
}
