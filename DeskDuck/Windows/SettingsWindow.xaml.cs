using DeskDuck.ViewModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.Graphics;

namespace DeskDuck
{
    /// <summary>
    /// Settings window that allows the user to configure all application options
    /// via SettingsViewModel and persists them to the user-specific appsettings.json.
    /// The window is always on top and has its title-bar icon removed for a cleaner look.
    /// </summary>
    public sealed partial class SettingsWindow : Window
    {
        /// <summary>Gets the view model driving this settings window.</summary>
        public SettingsViewModel ViewModel { get; }

        /// <summary>
        /// Initializes the settings window: sets the title, fixes the size, keeps it always
        /// on top, removes the window icon, and loads the current settings into the view model.
        /// </summary>
        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;

            Title = "DeskDuck Einstellungen";
            AppWindow.Resize(new SizeInt32(420, 600));

            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            OverlappedPresenter? presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }

            ViewModel.Load();
        }

        /// <summary>
        /// Saves current view model settings back to config, then closes the window.
        /// Shows an error dialog if saving fails.
        /// </summary>
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.Save();
                Close();
            }
            catch (Exception ex)
            {
                string configPath = ViewModel.ConfigPath;
                Debug.WriteLine($"[SettingsWindow] Error saving config: {ex.Message}");

                ContentDialog errorDialog = new()
                {
                    Title = "Error Saving Settings",
                    Content = $"An error occurred while saving the settings:\n\n{ex.Message}\n\nPath: {configPath}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        /// <summary>Closes the settings window without saving any changes.</summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
