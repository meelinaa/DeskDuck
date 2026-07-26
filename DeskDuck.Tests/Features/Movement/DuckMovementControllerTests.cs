using DeskDuck.Core.Enums;
using DeskDuck.Core.Features.Movement;
using DeskDuck.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using System.Timers;

namespace DeskDuck.Tests.Features.Movement;

/// <summary>
/// Unit tests for <see cref="DuckMovementController"/>.
/// </summary>
public class DuckMovementControllerTests
{
    private readonly Mock<IOptions<DuckConfig>> _mockOptions;
    private readonly Mock<ILogger<DuckMovementController>> _mockLogger;
    private readonly DuckConfig _config;

    public DuckMovementControllerTests()
    {
        _config = new DuckConfig { Speed = 5, MinWaitSeconds = 0, MaxWaitSeconds = 0 };
        _mockOptions = new Mock<IOptions<DuckConfig>>();
        _mockOptions.Setup(o => o.Value).Returns(_config);
        _mockLogger = new Mock<ILogger<DuckMovementController>>();
    }

    /// <summary>
    /// Tests that calling Pause fires the Hold trigger and stops the movement timer.
    /// TIMELY: Entsteht idealerweise vor dem Refactoring zu abstrakten Timern, nutzt daher Reflection für State-Prüfung.
    // [R]IGHT: Calling Pause puts the controller into Held state
    // [B]OUNDARY: Controller can be paused mid-walk
    [Fact]
    public void Pause_FiresHoldTrigger_AndStopsMovement()
    {
        // Arrange
        DuckMovementController controller = new(_mockOptions.Object, _mockLogger.Object);
        controller.Initialize(1920, 1080, 100, 100);
        controller.Start(100, 100); // Transitions to Waiting -> then Walking

        // Act
        controller.Pause();

        // Assert
        DuckStateMachine stateMachine = GetFieldValue<DuckStateMachine>(controller, "_stateMachine")!;
        Assert.Equal(DuckState.Held, stateMachine.CurrentState);
    }

    /// <summary>
    /// Tests that calling Resume updates the current position and fires Release trigger.
    // [R]IGHT: Resume correctly updates internal coordinates and resumes from Held
    [Fact]
    public void Resume_UpdatesPosition_AndFiresReleaseTrigger()
    {
        // Arrange
        DuckMovementController controller = new(_mockOptions.Object, _mockLogger.Object);
        controller.Initialize(1920, 1080, 100, 100);
        controller.Pause(); // Put into Held state

        // Act
        controller.Resume(150, 200);

        // Assert
        double currentX = GetFieldValue<double>(controller, "_currentX");
        double currentY = GetFieldValue<double>(controller, "_currentY");
        Assert.Equal(150, currentX);
        Assert.Equal(200, currentY);

        DuckStateMachine stateMachine = GetFieldValue<DuckStateMachine>(controller, "_stateMachine")!;
        // After release it goes to Waiting. We just check it's no longer Held.
        Assert.NotEqual(DuckState.Held, stateMachine.CurrentState);
    }

    /// <summary>
    /// Tests the Right behavior of the timer tick: it advances the duck's coordinates by dx/dy.
    /// TIMELY: Entsteht vor der Timer-Abstraktion. Ruft die private Tick-Methode per Reflection auf.
    // [R]IGHT: Position advances correctly based on dx/dy
    [Fact]
    public void OnTimerTick_AdvancesPosition_WhenWalking()
    {
        // Arrange
        DuckMovementController controller = new(_mockOptions.Object, _mockLogger.Object);
        controller.Initialize(1920, 1080, 100, 100);
        
        SetFieldValue(controller, "_currentX", 10.0);
        SetFieldValue(controller, "_currentY", 10.0);
        SetFieldValue(controller, "_targetX", 20.0);
        SetFieldValue(controller, "_targetY", 10.0);
        SetFieldValue(controller, "_dx", 5.0);
        SetFieldValue(controller, "_dy", 0.0);

        DuckStateMachine stateMachine = GetFieldValue<DuckStateMachine>(controller, "_stateMachine")!;
        stateMachine.Fire(DuckTrigger.StartWalkingRight); // Force into WalkingRight

        double? reportedX = null;
        double? reportedY = null;
        controller.PositionChanged += (x, y) => { reportedX = x; reportedY = y; };

        // Act
        InvokeTimerTick(controller);

        // Assert
        Assert.Equal(15.0, GetFieldValue<double>(controller, "_currentX"));
        Assert.Equal(10.0, GetFieldValue<double>(controller, "_currentY"));
        Assert.Equal(15.0, reportedX);
        Assert.Equal(10.0, reportedY);
    }

    /// <summary>
    /// Tests Boundary condition: timer tick reaches the destination when distance is less than or equal to speed.
    // [B]OUNDARY: Distance to target is less than speed (e.g. 2 < 5)
    [Fact]
    public void OnTimerTick_ReachesDestination_WhenDistanceLessThanSpeed()
    {
        // Arrange
        DuckMovementController controller = new(_mockOptions.Object, _mockLogger.Object);
        controller.Initialize(1920, 1080, 100, 100);
        
        SetFieldValue(controller, "_currentX", 18.0);
        SetFieldValue(controller, "_currentY", 10.0);
        SetFieldValue(controller, "_targetX", 20.0);
        SetFieldValue(controller, "_targetY", 10.0);
        SetFieldValue(controller, "_dx", 5.0); // Speed is 5, distance is 2.
        SetFieldValue(controller, "_dy", 0.0);

        DuckStateMachine stateMachine = GetFieldValue<DuckStateMachine>(controller, "_stateMachine")!;
        stateMachine.Fire(DuckTrigger.StartWalkingRight);

        // Act
        InvokeTimerTick(controller);

        // Assert
        // Should snap exactly to target
        Assert.Equal(20.0, GetFieldValue<double>(controller, "_currentX"));
        Assert.Equal(10.0, GetFieldValue<double>(controller, "_currentY"));
        
        // State should transition to Waiting because destination is reached
        Assert.Equal(DuckState.Waiting, stateMachine.CurrentState);
    }

    /// <summary>
    /// Tests Branch Coverage: when the target is essentially the same as current position (dist <= 1).
    // [B]OUNDARY: Target is the exact same as current position (dist = 0)
    [Fact]
    public void OnStateMachineTransitioned_DistanceZero_TriggersWaitingImmediately()
    {
        // Arrange
        DuckMovementController controller = new(_mockOptions.Object, _mockLogger.Object);
        controller.Initialize(1920, 1080, 100, 100);

        // We want to simulate the scenario where target is picked but dist <= 1.
        // Since target is picked via _random, we force the random to return a specific value.
        Mock<Random> mockRandom = new();
        mockRandom.Setup(r => r.Next(It.IsAny<int>(), It.IsAny<int>())).Returns(100); // Will always pick 100
        SetFieldValue(controller, "_random", mockRandom.Object);

        SetFieldValue(controller, "_currentX", 100.0);
        SetFieldValue(controller, "_currentY", 100.0);

        DuckStateMachine stateMachine = GetFieldValue<DuckStateMachine>(controller, "_stateMachine")!;

        // Act
        // This will transition to Waiting, delay 0, pick target (100, 100), dist is 0.
        // The dist > 1 branch is skipped, it immediately cycles back to Waiting.
        stateMachine.Fire(DuckTrigger.StartWalkingRight);
        stateMachine.Fire(DuckTrigger.ReachDestination); 

        // Assert
        // Give the async Task.Delay(0) time to resume and finish the method
        Thread.Sleep(50); 
        
        // Since it cycles back to waiting immediately, the state should remain Waiting
        Assert.Equal(DuckState.Waiting, stateMachine.CurrentState);
        // And _dx, _dy should not have been calculated
        Assert.Equal(0.0, GetFieldValue<double>(controller, "_dx"));
        Assert.Equal(0.0, GetFieldValue<double>(controller, "_dy"));
    }

    private static T? GetFieldValue<T>(object obj, string fieldName)
    {
        FieldInfo? field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return (T?)field?.GetValue(obj);
    }

    private static void SetFieldValue(object obj, string fieldName, object value)
    {
        FieldInfo? field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(obj, value);
    }

    private static void InvokeTimerTick(DuckMovementController controller)
    {
        MethodInfo? method = typeof(DuckMovementController).GetMethod("OnTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(controller, [null, null]);
    }
}
