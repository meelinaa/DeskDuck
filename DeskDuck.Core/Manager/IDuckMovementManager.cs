using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using DeskDuck.Enums;

namespace DeskDuck.Manager
{
    public interface IDuckMovementManager
    {
        event Action<DuckState>? StateChanged;
        event Action<int, int>? PositionChanged;

        void Initialize(AppWindow appWindow, DispatcherQueue dispatcherQueue);
        void Pause();
        void Resume();
        void Stop();
        void Start();
        void TeleportTo(double x, double y);
    }
}
