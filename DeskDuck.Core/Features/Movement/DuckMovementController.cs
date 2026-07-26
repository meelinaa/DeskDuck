using DeskDuck.Core.Enums;

using DeskDuck.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Timers;

namespace DeskDuck.Core.Features.Movement;

/// <summary>
/// Manages the autonomous movement of the duck overlay window across the desktop.
/// The duck picks a random target position, walks towards it at a configurable speed,
/// then waits for a random interval before choosing the next destination.
///
/// This class is entirely free of WinUI dependencies. It calculates screen coordinates
/// and fires events — the UI layer (MainWindow code-behind) physically moves the window.
/// Movement is driven by a <see cref="System.Timers.Timer"/> running at ~60 fps
/// on a thread-pool thread, which is safe because this class only mutates its own state.
/// </summary>
public class DuckMovementController : IDuckMovementController
{
    private readonly Random _random = new();
    private readonly DuckConfig _config;
    private readonly ILogger<DuckMovementController> _logger;
    private readonly DuckStateMachine _stateMachine;

    // Screen and duck bounds — set by Initialize()
    private int _screenWidth;
    private int _screenHeight;
    private int _duckWidth;
    private int _duckHeight;

    // Current calculated position
    private double _currentX;
    private double _currentY;

    // Target position and direction vector for the current walk
    private double _targetX;
    private double _targetY;
    private double _dx;
    private double _dy;

    // Timer that drives the movement loop (approx. 60 fps)
    private System.Timers.Timer? _movementTimer;

    // CancellationTokenSource that controls the async wait between walks
    private CancellationTokenSource? _waitCts;

    /// <summary>Raised whenever the duck transitions to a new <see cref="DuckState"/>.</summary>
    public event Action<DuckState>? StateChanged;

    /// <summary>
    /// Raised every movement tick with the duck's current screen coordinates.
    /// Subscribers (typically MainWindow.xaml.cs) move the window to these coordinates.
    /// </summary>
    public event Action<int, int>? PositionChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckMovementController"/> class.
    /// </summary>
    /// <param name="config">Configuration options for duck movement logic.</param>
    /// <param name="logger">Logger for recording state transitions and errors.</param>
    public DuckMovementController(IOptions<DuckConfig> config, ILogger<DuckMovementController> logger)
    {
        _config = config.Value;
        _logger = logger;
        _stateMachine = new DuckStateMachine();
        _stateMachine.OnStateChanged += OnStateMachineTransitioned;
    }

    /// <summary>
    /// Stores screen and duck dimensions for use in movement calculations.
    /// Creates the movement timer. Must be called before <see cref="Start"/>.
    /// </summary>
    public void Initialize(int screenWidth, int screenHeight, int duckWidth, int duckHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _duckWidth = duckWidth;
        _duckHeight = duckHeight;

        _movementTimer = new System.Timers.Timer(interval: 16); // ~60 fps
        _movementTimer.Elapsed += OnTimerTick;
        _movementTimer.AutoReset = true;
        OnStateMachineTransitioned(_stateMachine.CurrentState);
    }

    /// <summary>
    /// Temporarily halts the movement timer without resetting the current path.
    /// Used during drag operations so the duck does not fight user input.
    /// </summary>
    public void Pause()
    {
        _stateMachine.Fire(DuckTrigger.Hold);
    }

    /// <summary>
    /// Resumes autonomous movement after a pause. Updates internal position from
    /// the provided coordinates so the path continues from the window's actual location.
    /// </summary>
    /// <param name="currentX">The current X position of the duck window after the drag.</param>
    /// <param name="currentY">The current Y position of the duck window after the drag.</param>
    public void Resume(double currentX, double currentY)
    {
        _currentX = currentX;
        _currentY = currentY;
        _stateMachine.Fire(DuckTrigger.Release);
    }

    /// <summary>
    /// Completely stops autonomous movement and cancels any pending wait task.
    /// </summary>
    public void Stop()
    {
        _stateMachine.Fire(DuckTrigger.Stop);
    }

    /// <summary>
    /// Starts autonomous movement from the given initial position.
    /// </summary>
    /// <param name="currentX">The initial X position of the duck window.</param>
    /// <param name="currentY">The initial Y position of the duck window.</param>
    public void Start(double currentX, double currentY)
    {
        _currentX = currentX;
        _currentY = currentY;
        _stateMachine.Fire(DuckTrigger.Resume);
        PositionChanged?.Invoke((int)_currentX, (int)_currentY);
    }

    /// <summary>
    /// Teleports the duck instantly to the specified screen coordinates and fires
    /// <see cref="PositionChanged"/> so the UI layer can move the window immediately.
    /// Resets the walk state to Waiting so the next walk begins from the new position.
    /// </summary>
    public void TeleportTo(double x, double y)
    {
        _currentX = x;
        _currentY = y;
        PositionChanged?.Invoke((int)x, (int)y);

        if (_stateMachine.CurrentState == DuckState.WalkingLeft || _stateMachine.CurrentState == DuckState.WalkingRight)
        {
            _stateMachine.Fire(DuckTrigger.ReachDestination);
        }
        else if (_stateMachine.CurrentState == DuckState.Waiting)
        {
            // The wait task is currently delaying. To abort the ongoing delay and force a new random destination 
            // from the new teleported coordinates, we transition through Walking and immediately trigger ReachDestination.
            _stateMachine.Fire(DuckTrigger.StartWalkingLeft);
            _stateMachine.Fire(DuckTrigger.ReachDestination);
        }
    }

    /// <summary>
    /// Handles state machine transitions. Starts the timer when walking begins,
    /// stops it when the duck is held or stopped, and picks a new random destination
    /// after each wait period.
    /// </summary>
    private async void OnStateMachineTransitioned(DuckState state)
    {
        try
        {
            StateChanged?.Invoke(state);

            if (state == DuckState.Waiting)
            {
                _movementTimer?.Stop();
                _waitCts?.Cancel();
                _waitCts?.Dispose();
                _waitCts = new CancellationTokenSource();
                CancellationToken token = _waitCts.Token;

                int waitMs = _random.Next(
                    _config.MinWaitSeconds * 1000,
                    (_config.MaxWaitSeconds + 1) * 1000);

                try
                {
                    await Task.Delay(waitMs, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (_stateMachine.CurrentState != DuckState.Waiting) return;

                // Pick a random target within screen bounds
                int maxX = Math.Max(1, _screenWidth - _duckWidth);
                int maxY = Math.Max(1, _screenHeight - _duckHeight);

                _targetX = _random.Next(0, maxX);
                _targetY = _random.Next(0, maxY);

                double distX = _targetX - _currentX;
                double distY = _targetY - _currentY;
                double dist = Math.Sqrt(distX * distX + distY * distY);

                if (dist > 1)
                {
                    _dx = (distX / dist) * _config.Speed;
                    _dy = (distY / dist) * _config.Speed;

                    _stateMachine.Fire(_targetX < _currentX
                        ? DuckTrigger.StartWalkingLeft
                        : DuckTrigger.StartWalkingRight);
                }
                else
                {
                    // Same position — immediately cycle back to waiting
                    _stateMachine.Fire(DuckTrigger.StartWalkingLeft);
                    _stateMachine.Fire(DuckTrigger.ReachDestination);
                }
            }
            else if (state == DuckState.WalkingLeft || state == DuckState.WalkingRight)
            {
                _movementTimer?.Start();
            }
            else if (state == DuckState.Held || state == DuckState.Stopped)
            {
                _waitCts?.Cancel();
                _movementTimer?.Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in state transition to {State}", state);
        }
    }

    /// <summary>
    /// Called every ~16 ms by the movement timer. Advances the duck's position toward the target
    /// and fires <see cref="PositionChanged"/> so the UI layer can reposition the window.
    /// Switches the duck to Waiting when it reaches its destination.
    /// </summary>
    private void OnTimerTick(object? sender, ElapsedEventArgs e)
    {
        if (_stateMachine.CurrentState != DuckState.WalkingLeft &&
            _stateMachine.CurrentState != DuckState.WalkingRight)
        {
            _movementTimer?.Stop();
            return;
        }

        _currentX += _dx;
        _currentY += _dy;

        double distX = _targetX - _currentX;
        double distY = _targetY - _currentY;
        double dist = Math.Sqrt(distX * distX + distY * distY);

        if (dist <= _config.Speed)
        {
            _currentX = _targetX;
            _currentY = _targetY;
            PositionChanged?.Invoke((int)_currentX, (int)_currentY);
            _stateMachine.Fire(DuckTrigger.ReachDestination);
        }
        else
        {
            PositionChanged?.Invoke((int)_currentX, (int)_currentY);
        }
    }
}
