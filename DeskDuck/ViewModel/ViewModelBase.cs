using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeskDuck.ViewModel
{
    /// <summary>
    /// Base class for all ViewModels, implementing INotifyPropertyChanged.
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the specified property name.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a backing field to a new value and raises PropertyChanged if the value changed.
        /// Returns true when the property was actually updated.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
