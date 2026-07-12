using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DeskDuck.ViewModel
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private string _duckImageUri = "ms-appx:///Assets/Duck/duck-sitting.gif";
        private string _notificationTitle = string.Empty;
        private string _notificationMessage = string.Empty;
        private Visibility _notificationVisibility = Visibility.Collapsed;
        private Visibility _titleVisibility = Visibility.Collapsed;
        private Brush _notificationTextBrush = new SolidColorBrush(Microsoft.UI.Colors.Black);

        public string DuckImageUri
        {
            get => _duckImageUri;
            set => SetProperty(ref _duckImageUri, value);
        }

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

        public string NotificationMessage
        {
            get => _notificationMessage;
            set => SetProperty(ref _notificationMessage, value);
        }

        public Visibility NotificationVisibility
        {
            get => _notificationVisibility;
            set => SetProperty(ref _notificationVisibility, value);
        }

        private string _coordinatesText = "X: 0, Y: 0";
        private Visibility _coordinatesVisibility = Visibility.Visible;

        public string CoordinatesText
        {
            get => _coordinatesText;
            set => SetProperty(ref _coordinatesText, value);
        }

        public Visibility CoordinatesVisibility
        {
            get => _coordinatesVisibility;
            set => SetProperty(ref _coordinatesVisibility, value);
        }

        public Visibility TitleVisibility
        {
            get => _titleVisibility;
            set => SetProperty(ref _titleVisibility, value);
        }

        public Brush NotificationTextBrush
        {
            get => _notificationTextBrush;
            set => SetProperty(ref _notificationTextBrush, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
