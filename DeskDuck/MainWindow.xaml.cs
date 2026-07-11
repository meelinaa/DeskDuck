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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            ConfigureOverlayWindow();
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

            // Fenster auf volle Bildschirmgröße setzen
            var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                0, 0,
                display.OuterBounds.Width,
                display.OuterBounds.Height));

            // Klickdurchlässig machen (Mausklicks gehen an darunterliegende Apps)
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
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
