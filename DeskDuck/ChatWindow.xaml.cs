using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace DeskDuck
{
    public sealed partial class ChatWindow : Window
    {
        #region Win32 Interop

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetClassLong")]
        private static extern uint SetClassLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetClassLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
            }
            else
            {
                return new IntPtr(SetClassLong32(hWnd, nIndex, (uint)dwNewLong.ToInt32()));
            }
        }

        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int GCLP_HICONSM = -34;
        private const int GCL_HICON = -14;

        #endregion

        public ChatViewModel ViewModel { get; } = new();

        public ChatWindow()
        {
            this.InitializeComponent();

            this.Title = "Chat mit DeskDuck AI";
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(400, 550));

            // Set TitleBar PreferredTheme to match application mode
            this.AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            // Make the chat window always on top of other windows
            var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }

            // Remove the icon in the top-left corner
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
            SetClassLong(hwnd, GCLP_HICONSM, IntPtr.Zero);
            SetClassLong(hwnd, GCL_HICON, IntPtr.Zero);

            this.Activated += ChatWindow_Activated;
        }

        private bool _isLoaded = false;
        private async void ChatWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (!_isLoaded)
            {
                _isLoaded = true;
                await ViewModel.LoadModelsAsync();
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendCurrentMessage();
        }

        private async void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await SendCurrentMessage();
            }
        }

        private async System.Threading.Tasks.Task SendCurrentMessage()
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text)) return;

            // Send message and get response
            var sendTask = ViewModel.SendMessageAsync();
            
            // Scroll to user message immediately
            ScrollToBottom();
            
            // Wait for completion (typing delay)
            await sendTask;

            // Scroll to AI response
            ScrollToBottom();

            // Focus back to input text box
            InputTextBox.Focus(FocusState.Programmatic);
        }

        private void ScrollToBottom()
        {
            if (ViewModel.Messages.Count > 0)
            {
                MessagesList.ScrollIntoView(ViewModel.Messages[ViewModel.Messages.Count - 1]);
            }
        }
    }
}
