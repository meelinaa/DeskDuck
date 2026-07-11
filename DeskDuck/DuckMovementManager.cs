using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DeskDuck
{
    public enum DuckState
    {
        WalkingLeft,
        WalkingRight,
        Waiting,
        Held
    }

    public class DuckConfig
    {
        public double Speed { get; set; } = 2.0;
        public int MinWaitSeconds { get; set; } = 5;
        public int MaxWaitSeconds { get; set; } = 15;
    }

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

        public event Action<DuckState>? StateChanged;
        public event Action<int, int>? PositionChanged;

        public void Pause()
        {
            _isPaused = true;
            _movementTimer.Stop();
        }

        public void Resume()
        {
            _isPaused = false;
            // Sync internal coordinate tracker to current window position before starting next cycle
            _currentX = _appWindow.Position.X;
            _currentY = _appWindow.Position.Y;
            StartNextCycle();
        }

        public DuckMovementManager(AppWindow appWindow, DispatcherQueue dispatcherQueue)
        {
            _appWindow = appWindow;
            LoadConfig();

            // Init position at center of primary display
            var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            _currentX = (display.OuterBounds.Width - _appWindow.Size.Width) / 2;
            _currentY = (display.OuterBounds.Height - _appWindow.Size.Height) / 2;
            _appWindow.Move(new PointInt32((int)_currentX, (int)_currentY));

            // Setup movement loop timer (approx. 60 FPS)
            _movementTimer = dispatcherQueue.CreateTimer();
            _movementTimer.Interval = TimeSpan.FromMilliseconds(16);
            _movementTimer.Tick += OnTimerTick;
        }

        private void LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var loaded = JsonSerializer.Deserialize<DuckConfig>(json);
                    if (loaded != null)
                    {
                        _config = loaded;
                    }
                }
            }
            catch
            {
                // Fallback to defaults on error
            }
        }

        public void Start()
        {
            PositionChanged?.Invoke((int)_currentX, (int)_currentY);
            StartNextCycle();
        }

        private async void StartNextCycle()
        {
            _isMoving = false;
            _movementTimer.Stop();

            // Set state to waiting when stopping
            StateChanged?.Invoke(DuckState.Waiting);

            // Wait for a random duration between MinWaitSeconds and MaxWaitSeconds
            int waitTimeMs = _random.Next(_config.MinWaitSeconds * 1000, (_config.MaxWaitSeconds + 1) * 1000);
            await Task.Delay(waitTimeMs);

            // Choose a new target position within display bounds
            var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            int maxX = display.OuterBounds.Width - _appWindow.Size.Width;
            int maxY = display.OuterBounds.Height - _appWindow.Size.Height;

            _targetX = _random.Next(0, Math.Max(1, maxX));
            _targetY = _random.Next(0, Math.Max(1, maxY));

            // Determine state based on direction of travel
            DuckState moveState = _targetX < _currentX ? DuckState.WalkingLeft : DuckState.WalkingRight;

            // Calculate direction vector
            double distanceX = _targetX - _currentX;
            double distanceY = _targetY - _currentY;
            double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

            if (distance > 1)
            {
                _dx = (distanceX / distance) * _config.Speed;
                _dy = (distanceY / distance) * _config.Speed;
                _isMoving = true;
                
                // Raise event for state change before timer starts
                StateChanged?.Invoke(moveState);
                _movementTimer.Start();
            }
            else
            {
                // Already at destination or too close
                StartNextCycle();
            }
        }

        private void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_isPaused || !_isMoving) return;

            // Move step
            _currentX += _dx;
            _currentY += _dy;

            // Check if destination is reached
            double distanceX = _targetX - _currentX;
            double distanceY = _targetY - _currentY;
            double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

            // If we are close or have overshot, snap to target and start wait cycle
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
    }
}
