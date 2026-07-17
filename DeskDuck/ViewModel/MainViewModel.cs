using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DeskDuck.ViewModel
{
    /// <summary>
    /// View model for the main overlay window. Exposes bindable properties for the
    /// duck animation URI, notification content, notification visibility, coordinate
    /// display, and the title bar visibility of notifications.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private string _duckImageUri = "ms-appx:///Assets/Duck/duck-sitting.gif";
        private string _notificationTitle = string.Empty;
        private string _notificationMessage = string.Empty;
        private Visibility _notificationVisibility = Visibility.Collapsed;
        private Visibility _titleVisibility = Visibility.Collapsed;
        private Brush _notificationTextBrush = new SolidColorBrush(Microsoft.UI.Colors.Black);

        /// <summary>
        /// ms-appx URI of the GIF asset currently shown for the duck.
        /// Updated whenever the duck transitions to a new <see cref="DeskDuck.Enums.DuckState"/>.
        /// </summary>
        public string DuckImageUri
        {
            get => _duckImageUri;
            set => SetProperty(ref _duckImageUri, value);
        }

        /// <summary>
        /// Optional title for the notification bubble.
        /// Setting this property also auto-updates <see cref="TitleVisibility"/> so an
        /// empty title collapses the title row in the UI without extra binding logic.
        /// </summary>
        public string NotificationTitle
        {
            get => _notificationTitle;
            set
            {
                if (SetProperty(ref _notificationTitle, value))
                {
                    TitleVisibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        /// <summary>The main body text of the notification bubble.</summary>
        public string NotificationMessage
        {
            get => _notificationMessage;
            set => SetProperty(ref _notificationMessage, value);
        }

        /// <summary>Controls whether the notification bubble is shown or hidden.</summary>
        public Visibility NotificationVisibility
        {
            get => _notificationVisibility;
            set => SetProperty(ref _notificationVisibility, value);
        }

        private string _coordinatesText = "X: 0, Y: 0";
        private Visibility _coordinatesVisibility = Visibility.Visible;

        /// <summary>Formatted string showing the duck's current screen position (e.g. "X: 120, Y: 450").</summary>
        public string CoordinatesText
        {
            get => _coordinatesText;
            set => SetProperty(ref _coordinatesText, value);
        }

        /// <summary>
        /// Controls whether the coordinate label beneath the duck is visible.
        /// Driven by the ShowCoordinates setting in appsettings.json.
        /// </summary>
        public Visibility CoordinatesVisibility
        {
            get => _coordinatesVisibility;
            set => SetProperty(ref _coordinatesVisibility, value);
        }

        /// <summary>
        /// Visibility of the notification title row. Automatically collapsed when
        /// <see cref="NotificationTitle"/> is set to an empty or whitespace-only string.
        /// </summary>
        public Visibility TitleVisibility
        {
            get => _titleVisibility;
            set => SetProperty(ref _titleVisibility, value);
        }

        /// <summary>
        /// Foreground brush for the notification text. Set by the notification source
        /// to visually distinguish severity (red for warnings, blue for info, black otherwise).
        /// </summary>
        public Brush NotificationTextBrush
        {
            get => _notificationTextBrush;
            set => SetProperty(ref _notificationTextBrush, value);
        }

    }
}
