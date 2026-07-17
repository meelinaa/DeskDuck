using System;
using DeskDuck.Helper;

namespace DeskDuck.Features.Shell
{
    public interface IWindowService
    {
        void ConfigureOverlayStyles(IntPtr hwnd);
        bool RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint vk);
        bool UnregisterHotkey(IntPtr hwnd, int id);
        bool RegisterSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id, IntPtr refData);
        bool RemoveSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id);
        IntPtr DefaultSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        bool GetCursorPosition(out Win32WindowHelper.PointStruct point);

        int HotkeyId { get; }
        uint ModControl { get; }
        uint ModAlt { get; }
        uint ModShift { get; }
        uint VkD { get; }
        uint WmHotkey { get; }
    }
}
