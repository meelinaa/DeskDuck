using System;
using DeskDuck.Helper;

namespace DeskDuck.Features.Shell
{
    public class WindowService : IWindowService
    {
        public int HotkeyId => Win32WindowHelper.HOTKEY_ID;
        public uint ModControl => Win32WindowHelper.MOD_CONTROL;
        public uint ModAlt => Win32WindowHelper.MOD_ALT;
        public uint ModShift => Win32WindowHelper.MOD_SHIFT;
        public uint VkD => Win32WindowHelper.VK_D;
        public uint WmHotkey => Win32WindowHelper.WM_HOTKEY;

        public void ConfigureOverlayStyles(IntPtr hwnd)
        {
            Win32WindowHelper.ConfigureOverlayStyles(hwnd);
        }

        public IntPtr DefaultSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return Win32WindowHelper.DefaultSubclassProc(hwnd, msg, wParam, lParam);
        }

        public bool GetCursorPosition(out Win32WindowHelper.PointStruct point)
        {
            return Win32WindowHelper.GetCursorPosition(out point);
        }

        public bool RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint vk)
        {
            return Win32WindowHelper.RegisterHotkey(hwnd, id, modifiers, vk);
        }

        public bool RegisterSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id, IntPtr refData)
        {
            return Win32WindowHelper.RegisterSubclass(hwnd, proc, id, refData);
        }

        public bool RemoveSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id)
        {
            return Win32WindowHelper.RemoveSubclass(hwnd, proc, id);
        }

        public bool UnregisterHotkey(IntPtr hwnd, int id)
        {
            return Win32WindowHelper.UnregisterHotkey(hwnd, id);
        }
    }
}
