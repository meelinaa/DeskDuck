using System;
using System.Runtime.InteropServices;

namespace DeskDuck.Helper
{
    /// <summary>
    /// Provides helper methods for configuring window styles, hotkeys, subclasses,
    /// and system metrics using Win32 API.
    /// </summary>
    public static class Win32WindowHelper
    {
        #region Win32 P/Invokes

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out PointStruct lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        #endregion

        #region Constants

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public const int HOTKEY_ID = 1337;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint VK_D = 0x44;
        public const uint WM_HOTKEY = 0x0312;

        #endregion

        #region Structures & Classes

        /// <summary>
        /// Struct representing a 2D point (X, Y) in screen coordinates.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PointStruct
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Delegate matching the SUBCLASSPROC signature for Win32 window subclass callbacks.
        /// </summary>
        public delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        /// <summary>
        /// Mirrors the Win32 MEMORYSTATUSEX structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        /// <summary>
        /// Mirrors the Win32 FILETIME structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
            public readonly ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
        }

        #endregion

        /// <summary>
        /// Retrieves the current cursor position in screen coordinates.
        /// </summary>
        public static bool GetCursorPosition(out PointStruct lpPoint)
        {
            return GetCursorPos(out lpPoint);
        }

        /// <summary>
        /// Sets window styles to make the window transparent to mouse clicks (click-through).
        /// </summary>
        public static void MakeClickThrough(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        /// <summary>
        /// Restores window styles to make the window interactive (clickable) again.
        /// </summary>
        public static void MakeInteractive(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
        }

        /// <summary>
        /// Configures initial overlay window styles (always on top, tool window, no activation).
        /// </summary>
        public static void ConfigureOverlayStyles(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        /// <summary>
        /// Registers a global hotkey with Windows.
        /// </summary>
        public static bool RegisterHotkey(IntPtr hWnd, int id, uint fsModifiers, uint vk)
        {
            return RegisterHotKey(hWnd, id, fsModifiers, vk);
        }

        /// <summary>
        /// Unregisters a global hotkey.
        /// </summary>
        public static bool UnregisterHotkey(IntPtr hWnd, int id)
        {
            return UnregisterHotKey(hWnd, id);
        }

        /// <summary>
        /// Registers a window subclass callback.
        /// </summary>
        public static bool RegisterSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            return SetWindowSubclass(hWnd, pfnSubclass, uIdSubclass, dwRefData);
        }

        /// <summary>
        /// Removes a window subclass callback.
        /// </summary>
        public static bool RemoveSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass)
        {
            return RemoveWindowSubclass(hWnd, pfnSubclass, uIdSubclass);
        }

        /// <summary>
        /// Passes subclass messages to the default window procedure.
        /// </summary>
        public static IntPtr DefaultSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        /// <summary>
        /// Retrieves the current system-wide memory load percentage.
        /// </summary>
        public static bool GetMemoryLoad(out uint memoryLoad)
        {
            memoryLoad = 0;
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                memoryLoad = memStatus.dwMemoryLoad;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Retrieves system timing information (idle, kernel, and user times).
        /// </summary>
        public static bool GetSystemTimesInfo(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime)
        {
            return GetSystemTimes(out lpIdleTime, out lpKernelTime, out lpUserTime);
        }
    }
}
