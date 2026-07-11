using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DeskDuck
{
    /// <summary>
    /// Transparent overlay window that displays a walking duck on the desktop.
    /// The window is click-through so you can interact with apps underneath.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        #region Win32 Interop
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out PointStruct lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct PointStruct
        {
            public int X;
            public int Y;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        #endregion

        private DuckMovementManager? _movementManager;
        private RabbitMQBackgroundService? _rabbitMQService;

        public MainViewModel ViewModel { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            ConfigureOverlayWindow();

            _movementManager = new DuckMovementManager(this.AppWindow, this.DispatcherQueue);
            _movementManager.StateChanged += OnDuckStateChanged;
            _movementManager.PositionChanged += OnDuckPositionChanged;
            _movementManager.Start();

            // Start RabbitMQ Background Service
            _rabbitMQService = new RabbitMQBackgroundService(
                this.DispatcherQueue,
                ShowNotification,
                HideNotification
            );
            _rabbitMQService.Start();

            this.Closed += OnWindowClosed;
        }

        private async void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (_rabbitMQService != null)
            {
                await _rabbitMQService.StopAsync();
            }
        }

        private void ShowNotification(NotificationMessage message)
        {
            ViewModel.NotificationTitle = message.Title ?? string.Empty;
            ViewModel.NotificationMessage = message.Message;
            ViewModel.NotificationVisibility = Visibility.Visible;
        }

        private void HideNotification()
        {
            ViewModel.NotificationVisibility = Visibility.Collapsed;
        }

        private bool _isDragging = false;
        private Windows.Graphics.PointInt32 _dragStartWindowPos;
        private PointStruct _dragStartCursorPos;

        private void DuckImage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_movementManager == null) return;

            _isDragging = true;
            _movementManager.Pause();
            UpdateDuckVisual(DuckState.Held);

            GetCursorPos(out _dragStartCursorPos);
            _dragStartWindowPos = new Windows.Graphics.PointInt32(this.AppWindow.Position.X, this.AppWindow.Position.Y);

            (sender as UIElement)?.CapturePointer(e.Pointer);
        }

        private void DuckImage_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            GetCursorPos(out var currentCursorPos);
            var deltaX = currentCursorPos.X - _dragStartCursorPos.X;
            var deltaY = currentCursorPos.Y - _dragStartCursorPos.Y;

            var newX = _dragStartWindowPos.X + deltaX;
            var newY = _dragStartWindowPos.Y + deltaY;

            this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            ViewModel.CoordinatesText = $"X: {newX}, Y: {newY}";
        }

        private void DuckImage_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            (sender as UIElement)?.ReleasePointerCapture(e.Pointer);

            UpdateDuckVisual(DuckState.Waiting);
            _movementManager?.Resume();
        }

        private void OnDuckStateChanged(DuckState state)
        {
            UpdateDuckVisual(state);
        }

        private void OnDuckPositionChanged(int x, int y)
        {
            ViewModel.CoordinatesText = $"X: {x}, Y: {y}";
        }

        private void UpdateDuckVisual(DuckState state)
        {
            string uriString = state switch
            {
                DuckState.WalkingLeft => "ms-appx:///Assets/Duck/duck-walking-to-left.gif",
                DuckState.WalkingRight => "ms-appx:///Assets/Duck/duck-walking-to-right.gif",
                DuckState.Held => "ms-appx:///Assets/Duck/pokeball.gif",
                _ => "ms-appx:///Assets/Duck/duck-sitting.gif"
            };

            ViewModel.DuckImageUri = uriString;
        }

        private void ConfigureOverlayWindow()
        {
            // Transparenter Hintergrund via custom SystemBackdrop
            SystemBackdrop = new TransparentBackdrop();

            var appWindow = this.AppWindow;
            var hwnd = WindowNative.GetWindowHandle(this);

            // Rahmenlos und immer im Vordergrund
            var presenter = OverlappedPresenter.CreateForToolWindow();
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
            appWindow.SetPresenter(presenter);

            // Fenstergröße auf kompakte Ente-Größe setzen
            appWindow.Resize(new Windows.Graphics.SizeInt32(300, 300));

            // Klickdurchlässig machen NUR für transparente Bereiche.
            // Ohne WS_EX_TRANSPARENT können sichtbare UI-Elemente wie die Ente Mausklicks empfangen.
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }
    }

    /// <summary>
    /// Custom SystemBackdrop that renders a fully transparent background,
    /// allowing the desktop and other windows to show through.
    /// </summary>
    public class TransparentBackdrop : SystemBackdrop
    {
        protected override void OnTargetConnected(
            ICompositionSupportsSystemBackdrop connectedTarget,
            XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            // In WinUI 3, ICompositionSupportsSystemBackdrop is a WinRT interface.
            // Under the hood, we can retrieve the Windows.UI.Composition.Compositor.
            if (connectedTarget is Windows.UI.Composition.CompositionObject compositionObject)
            {
                var compositor = compositionObject.Compositor;
                var transparentBrush = compositor.CreateColorBrush(
                    new Windows.UI.Color { A = 0, R = 0, G = 0, B = 0 });
                connectedTarget.SystemBackdrop = transparentBrush;
            }
        }
    }
}
