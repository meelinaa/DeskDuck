using Microsoft.UI.Windowing;

namespace DeskDuck.Features.Shell
{
    public interface IDuckWindowManager
    {
        void Initialize(AppWindow duckAppWindow);
        void OpenChatWindow();
        void OpenSettingsWindow();
        void CloseAll();
    }
}
