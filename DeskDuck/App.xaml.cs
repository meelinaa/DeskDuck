using Microsoft.UI.Xaml;

namespace DeskDuck
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// Entry point for the WinUI 3 application; creates and activates the main overlay window.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object. This is the first authored code to
        /// execute, equivalent to main() or WinMain() in a traditional Win32 application.
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched. Creates the transparent overlay window
        /// and activates it so the duck appears on the desktop immediately.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
