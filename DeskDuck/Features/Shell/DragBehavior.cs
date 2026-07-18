using DeskDuck.Core.Features.Shell;
using DeskDuck.Core.Helper;
using DeskDuck.Core.Manager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace DeskDuck.Features.Shell
{
    public static class DragBehavior
    {
        public static readonly DependencyProperty IsDraggableProperty =
            DependencyProperty.RegisterAttached(
                "IsDraggable",
                typeof(bool),
                typeof(DragBehavior),
                new PropertyMetadata(false, OnIsDraggableChanged));

        public static bool GetIsDraggable(DependencyObject obj) => (bool)obj.GetValue(IsDraggableProperty);
        public static void SetIsDraggable(DependencyObject obj, bool value) => obj.SetValue(IsDraggableProperty, value);

        private static bool _isDragging = false;
        private static PointInt32 _dragStartWindowPos;
        private static Win32WindowHelper.PointStruct _dragStartCursorPos;

        private static void OnIsDraggableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.PointerPressed += Element_PointerPressed;
                    element.PointerMoved += Element_PointerMoved;
                    element.PointerReleased += Element_PointerReleased;
                }
                else
                {
                    element.PointerPressed -= Element_PointerPressed;
                    element.PointerMoved -= Element_PointerMoved;
                    element.PointerReleased -= Element_PointerReleased;
                }
            }
        }

        private static void Element_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not UIElement element) return;

            var properties = e.GetCurrentPoint(element).Properties;
            
            var app = (App)Application.Current;
            var movementManager = app.Host.Services.GetRequiredService<IDuckMovementManager>();

            if (properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                movementManager.Pause();

                var windowService = app.Host.Services.GetRequiredService<IWindowService>();
                var mainWindow = app.Host.Services.GetRequiredService<MainWindow>();

                if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                {
                    windowService.GetCursorPosition(out _dragStartCursorPos);
                }
                
                _dragStartWindowPos = new PointInt32(mainWindow.AppWindow.Position.X, mainWindow.AppWindow.Position.Y);
                element.CapturePointer(e.Pointer);
            }
            else if (properties.IsRightButtonPressed)
            {
                movementManager.Pause();
                if (sender is FrameworkElement frameworkElement)
                {
                    var flyout = FlyoutBase.GetAttachedFlyout(frameworkElement);
                    if (flyout != null)
                    {
                        flyout.Closed -= Flyout_Closed;
                        flyout.Closed += Flyout_Closed;
                        flyout.ShowAt(frameworkElement);
                    }
                }
            }
        }

        private static void Flyout_Closed(object? sender, object e)
        {
            if (sender is FlyoutBase flyout)
            {
                flyout.Closed -= Flyout_Closed;
            }

            var app = (App)Application.Current;
            var movementManager = app.Host.Services.GetRequiredService<IDuckMovementManager>();
            movementManager.Resume();
        }

        private static void Element_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) return;

            var app = (App)Application.Current;
            var windowService = app.Host.Services.GetRequiredService<IWindowService>();
            var mainWindow = app.Host.Services.GetRequiredService<MainWindow>();
            
            windowService.GetCursorPosition(out Win32WindowHelper.PointStruct currentCursorPos);
            int deltaX = currentCursorPos.X - _dragStartCursorPos.X;
            int deltaY = currentCursorPos.Y - _dragStartCursorPos.Y;

            int newX = _dragStartWindowPos.X + deltaX;
            int newY = _dragStartWindowPos.Y + deltaY;

            mainWindow.AppWindow.Move(new PointInt32(newX, newY));
        }

        private static void Element_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            if (sender is not UIElement element) return;

            _isDragging = false;
            element.ReleasePointerCapture(e.Pointer);

            var app = (App)Application.Current;
            var movementManager = app.Host.Services.GetRequiredService<IDuckMovementManager>();
            movementManager.Resume();
        }
    }
}
