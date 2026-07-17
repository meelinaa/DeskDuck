using DeskDuck.Enums;
using DeskDuck.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    public class DuckMovementManager
    {
        private readonly AppWindow _appWindow;
        private readonly DispatcherQueueTimer _movementTimer;
        private readonly Random _random = new();
        private DuckConfig _config = new();

        private double _currentX;
        private double _currentY;
        private double _targetX;
        private double _targetY;
        private double _dx;
        private double _dy;
        private bool _isMoving;
        private bool _isPaused;
        private bool _isStopped;

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
            _isPaused = true;
            _movementTimer.Stop();
        }

        /// <summary>
        /// Resumes movement from the current window position after a pause.
        /// Syncs the internal coordinate tracker to the actual window position first
        /// to avoid a position jump if the window was moved while paused (e.g. via drag).
        /// Has no effect if the manager is in the fully stopped state.
        /// </summary>
        public void Resume()
        {
            if (_isStopped) return;
            _isPaused = false;
            _currentX = _appWindow.Position.X;
            _currentY = _appWindow.Position.Y;
            StartNextCycle();
        }

        /// <summary>
        /// Fully stops movement and emits a <see cref="DuckState.Waiting"/> state.
        /// Used while the chat or settings window is open so the duck stays in place.
        /// </summary>
        public void Stop()
        {
            _isStopped = true;
            _isMoving = false;
            _movementTimer.Stop();
            StateChanged?.Invoke(DuckState.Waiting);
        }

        /// <summary>
        /// Starts (or restarts) autonomous movement from the current position.
        /// </summary>
        public void Start()
        {
            _isStopped = false;
            Resume();
            PositionChanged?.Invoke((int)_currentX, (int)_currentY);
        }

        /// <summary>
        /// Initializes the movement manager by loading the duck configuration, placing the duck
        /// at the center of the primary display, and setting up a ~60 FPS dispatcher timer
        /// that drives each movement step.
        /// </summary>
        public DuckMovementManager(AppWindow appWindow, DispatcherQueue dispatcherQueue)
        {
            _appWindow = appWindow;
            LoadConfig();

            var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            _currentX = (display.OuterBounds.Width - _appWindow.Size.Width) / 2;
            _currentY = (display.OuterBounds.Height - _appWindow.Size.Height) / 2;
            _appWindow.Move(new PointInt32((int)_currentX, (int)_currentY));

            _movementTimer = dispatcherQueue.CreateTimer();
            _movementTimer.Interval = TimeSpan.FromMilliseconds(16);
            _movementTimer.Tick += OnTimerTick;
        }

        /// <summary>
        /// Reads movement parameters (speed, wait range) from the central appsettings.json.
        /// Silently falls back to the compiled defaults on any error.
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                _config = Helper.ConfigHelper.LoadSettings().Duck;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DuckMovementManager] Error loading config: {ex.Message}");
            }
        }

        /// <summary>
        /// Begins a new movement cycle: emits the Waiting state, waits for a random delay,
        /// then picks a random target position within the display bounds and starts the
        /// movement timer in the appropriate walking direction.
        /// If the duck is already at the target (distance &lt;= 1), the cycle restarts immediately.
        /// </summary>
        private async void StartNextCycle()
        {
            if (_isStopped) return;
            _isMoving = false;
            _movementTimer.Stop();

            StateChanged?.Invoke(DuckState.Waiting);

            int waitTimeMs = _random.Next(_config.MinWaitSeconds * 1000, (_config.MaxWaitSeconds + 1) * 1000);
            await Task.Delay(waitTimeMs);

            if (_isStopped) return;

            var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            int maxX = display.OuterBounds.Width - _appWindow.Size.Width;
            int maxY = display.OuterBounds.Height - _appWindow.Size.Height;

            _targetX = _random.Next(0, Math.Max(1, maxX));
            _targetY = _random.Next(0, Math.Max(1, maxY));

            DuckState moveState = _targetX < _currentX ? DuckState.WalkingLeft : DuckState.WalkingRight;

            double distanceX = _targetX - _currentX;
            double distanceY = _targetY - _currentY;
            double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

            if (distance > 1)
            {
                _dx = (distanceX / distance) * _config.Speed;
                _dy = (distanceY / distance) * _config.Speed;
                _isMoving = true;

                StateChanged?.Invoke(moveState);
                _movementTimer.Start();
            }
            else
            {
                StartNextCycle();
            }
        }

        /// <summary>
        /// Called at ~60 FPS by the dispatcher timer. Advances the duck's position by one step
        /// along the direction vector. When the remaining distance is within one step, the duck
        /// snaps to the target and begins the next wait-and-walk cycle.
        /// </summary>
        private void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_isStopped || _isPaused || !_isMoving) return;

            _currentX += _dx;
            _currentY += _dy;

            double distanceX = _targetX - _currentX;
            double distanceY = _targetY - _currentY;
            double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

            if (distance <= _config.Speed)
            {
                _currentX = _targetX;
                _currentY = _targetY;
                _appWindow.Move(new PointInt32((int)_currentX, (int)_currentY));
                PositionChanged?.Invoke((int)_currentX, (int)_currentY);
                StartNextCycle();
            }
            else
            {
                _appWindow.Move(new PointInt32((int)_currentX, (int)_currentY));
                PositionChanged?.Invoke((int)_currentX, (int)_currentY);
            }
        }

        /// <summary>
        /// Instantly moves the duck to the specified screen coordinates and, if the manager
        /// is active, restarts the random path selection from the new position.
        /// </summary>
        public void TeleportTo(double x, double y)
        {
            _currentX = x;
            _currentY = y;
            _appWindow.Move(new PointInt32((int)x, (int)y));
            PositionChanged?.Invoke((int)x, (int)y);

            if (!_isStopped && !_isPaused)
            {
                StartNextCycle();
            }
        }
    }
}
