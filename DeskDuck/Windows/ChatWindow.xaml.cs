using DeskDuck.ViewModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

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

        public ChatViewModel ChatViewModel { get; } = new();

        public ChatWindow()
        {
            InitializeComponent();

            Title = "Chat mit DeskDuck AI";
            AppWindow.Resize(new SizeInt32(400, 550));

            // Set TitleBar PreferredTheme to match application mode
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            // Make the chat window always on top of other windows
            var presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }

            // Remove the icon in the top-left corner
            var hwnd = WindowNative.GetWindowHandle(this);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
            SetClassLong(hwnd, GCLP_HICONSM, IntPtr.Zero);
            SetClassLong(hwnd, GCL_HICON, IntPtr.Zero);

            Activated += ChatWindow_Activated;
        }

        private bool _isLoaded = false;
        private async void ChatWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (!_isLoaded)
            {
                _isLoaded = true;
                await ChatViewModel.LoadModelsAsync();
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

        private async Task SendCurrentMessage()
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text)) 
                return;
            var sendTask = ChatViewModel.SendMessageAsync();
            ScrollToBottom();
            await sendTask;
            ScrollToBottom();
            InputTextBox.Focus(FocusState.Programmatic);
        }

        private void ScrollToBottom()
        {
            if (ChatViewModel.Messages.Count > 0)
            {
                MessagesList.ScrollIntoView(ChatViewModel.Messages[ChatViewModel.Messages.Count - 1]);
            }
        }
    }
}
