using DeskDuck.Enums;
using DeskDuck.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using System;
using System.Threading.Tasks;
using Windows.Graphics;

namespace DeskDuck.Manager
{
    /// <summary>
    /// Manages the autonomous movement of the duck overlay window across the desktop.
    /// The duck picks a random target position, walks towards it at a configurable speed,
    /// then waits for a random interval before choosing the next destination.
    /// Movement can be paused (e.g. during drag), stopped (e.g. while a modal window is open),
    /// or teleported to an explicit position via the hotkey.
    /// </summary>
    public class DuckMovementManager : IDuckMovementManager
    {
        private AppWindow? _appWindow;
        private DispatcherQueueTimer? _movementTimer;
        private readonly Random _random = new();
        private readonly DuckConfig _config;
        private readonly ILogger<DuckMovementManager> _logger;

        private double _currentX;
        private double _currentY;
        private double _targetX;
        private double _targetY;
        private double _dx;
        private double _dy;
        
        private readonly DuckStateMachine _stateMachine;
        private System.Threading.CancellationTokenSource? _waitCts;

        /// <summary>Raised whenever the duck transitions to a new <see cref="DuckState"/>.</summary>
        public event Action<DuckState>? StateChanged;

        /// <summary>Raised every tick with the duck's current screen coordinates.</summary>
        public event Action<int, int>? PositionChanged;

        /// <summary>
        /// Temporarily halts the movement timer without resetting the current path.
        /// Used during drag operations so the duck does not fight user input.
        /// </summary>
        public void Pause()
        {
            _stateMachine.Fire(DuckTrigger.Hold);
        }

        public void Resume()
        {
            if (_appWindow == null) return;
            
            _currentX = _appWindow.Position.X;
            _currentY = _appWindow.Position.Y;
            _stateMachine.Fire(DuckTrigger.Release);
        }

        public void Stop()
        {
            _stateMachine.Fire(DuckTrigger.Stop);
        }

        public void Start()
        {
            if (_appWindow == null) return;
            
            _currentX = _appWindow.Position.X;
            _currentY = _appWindow.Position.Y;
            
            // if already in waiting/walking, Resume does nothing or is ignored.
            // if Stopped, this transitions to Waiting.
            _stateMachine.Fire(DuckTrigger.Resume);
            
            PositionChanged?.Invoke((int)_currentX, (int)_currentY);
        }

        public DuckMovementManager(IOptions<DuckConfig> config, ILogger<DuckMovementManager> logger)
        {
            _config = config.Value;
            _logger = logger;
            _stateMachine = new DuckStateMachine();
            _stateMachine.OnStateChanged += OnStateMachineTransitioned;
        }

        public void Initialize(AppWindow appWindow, DispatcherQueue dispatcherQueue)
        {
            _appWindow = appWindow;

            var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            _currentX = (display.OuterBounds.Width - _appWindow.Size.Width) / 2;
            _currentY = (display.OuterBounds.Height - _appWindow.Size.Height) / 2;
            _appWindow.Move(new PointInt32((int)_currentX, (int)_currentY));

            _movementTimer = dispatcherQueue.CreateTimer();
            _movementTimer.Interval = TimeSpan.FromMilliseconds(16);
            _movementTimer.Tick += OnTimerTick;
            
            // Trigger initial state
            OnStateMachineTransitioned(_stateMachine.CurrentState);
        }

        private async void OnStateMachineTransitioned(DuckState state)
        {
            try
            {
                StateChanged?.Invoke(state);

                if (state == DuckState.Waiting)
                {
                    _movementTimer?.Stop();
                    _waitCts?.Cancel();
                    _waitCts = new System.Threading.CancellationTokenSource();
                    var token = _waitCts.Token;

                    int waitTimeMs = _random.Next(_config.MinWaitSeconds * 1000, (_config.MaxWaitSeconds + 1) * 1000);

                    try
                    {
                        await Task.Delay(waitTimeMs, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }

                    if (_stateMachine.CurrentState != DuckState.Waiting || _appWindow == null) return;

                    var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
                    int maxX = display.OuterBounds.Width - _appWindow.Size.Width;
                    int maxY = display.OuterBounds.Height - _appWindow.Size.Height;

                    _targetX = _random.Next(0, Math.Max(1, maxX));
                    _targetY = _random.Next(0, Math.Max(1, maxY));

                    double distanceX = _targetX - _currentX;
                    double distanceY = _targetY - _currentY;
                    double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

                    if (distance > 1)
                    {
                        _dx = (distanceX / distance) * _config.Speed;
                        _dy = (distanceY / distance) * _config.Speed;

                        if (_targetX < _currentX)
                            _stateMachine.Fire(DuckTrigger.StartWalkingLeft);
                        else
                            _stateMachine.Fire(DuckTrigger.StartWalkingRight);
                    }
                    else
                    {
                        // If target is same as current position, just wait again
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

        private void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_stateMachine.CurrentState != DuckState.WalkingLeft && _stateMachine.CurrentState != DuckState.WalkingRight)
            {
                _movementTimer?.Stop();
                return;
            }

            _currentX += _dx;
            _currentY += _dy;

            double distanceX = _targetX - _currentX;
            double distanceY = _targetY - _currentY;
            double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

            if (distance <= _config.Speed)
            {
                _currentX = _targetX;
                _currentY = _targetY;
                _appWindow?.Move(new PointInt32((int)_currentX, (int)_currentY));
                PositionChanged?.Invoke((int)_currentX, (int)_currentY);
                
                _stateMachine.Fire(DuckTrigger.ReachDestination);
            }
            else
            {
                _appWindow?.Move(new PointInt32((int)_currentX, (int)_currentY));
                PositionChanged?.Invoke((int)_currentX, (int)_currentY);
            }
        }

        public void TeleportTo(double x, double y)
        {
            _currentX = x;
            _currentY = y;
            _appWindow?.Move(new PointInt32((int)x, (int)y));
            PositionChanged?.Invoke((int)x, (int)y);

            if (_stateMachine.CurrentState == DuckState.WalkingLeft || _stateMachine.CurrentState == DuckState.WalkingRight)
            {
                _stateMachine.Fire(DuckTrigger.ReachDestination);
            }
            else if (_stateMachine.CurrentState == DuckState.Waiting)
            {
                // Just let it keep waiting, but force it to recalculate eventually
                _stateMachine.Fire(DuckTrigger.StartWalkingLeft);
                _stateMachine.Fire(DuckTrigger.ReachDestination);
            }
        }
    }
}
