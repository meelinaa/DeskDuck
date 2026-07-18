using Microsoft.UI.Windowing;

namespace DeskDuck.Core.Features.Shell;

public interface IDuckWindowManager
{
    void Initialize(AppWindow duckAppWindow);
    void OpenChatWindow();
    void OpenSettingsWindow();
    void CloseAll();
}
