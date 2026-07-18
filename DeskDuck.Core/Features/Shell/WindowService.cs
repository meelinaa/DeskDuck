using DeskDuck.Core.Helper;

namespace DeskDuck.Core.Features.Shell;

/// <summary>
/// Provides an implementation of <see cref="IWindowService"/> that delegates calls to <see cref="Win32WindowHelper"/>.
/// </summary>
public class WindowService : IWindowService
{
    /// <inheritdoc/>
    public int HotkeyId => Win32WindowHelper.HOTKEY_ID;
    /// <inheritdoc/>
    public uint ModControl => Win32WindowHelper.MOD_CONTROL;
    /// <inheritdoc/>
    public uint ModAlt => Win32WindowHelper.MOD_ALT;
    /// <inheritdoc/>
    public uint ModShift => Win32WindowHelper.MOD_SHIFT;
    /// <inheritdoc/>
    public uint VkD => Win32WindowHelper.VK_D;
    /// <inheritdoc/>
    public uint WmHotkey => Win32WindowHelper.WM_HOTKEY;

    /// <inheritdoc/>
    public void ConfigureOverlayStyles(IntPtr hwnd)
    {
        Win32WindowHelper.ConfigureOverlayStyles(hwnd);
    }

    /// <inheritdoc/>
    public IntPtr DefaultSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return Win32WindowHelper.DefaultSubclassProc(hwnd, msg, wParam, lParam);
    }

    /// <inheritdoc/>
    public bool GetCursorPosition(out Win32WindowHelper.PointStruct point)
    {
        return Win32WindowHelper.GetCursorPosition(out point);
    }

    /// <inheritdoc/>
    public bool RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint vk)
    {
        return Win32WindowHelper.RegisterHotkey(hwnd, id, modifiers, vk);
    }

    /// <inheritdoc/>
    public bool RegisterSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id, IntPtr refData)
    {
        return Win32WindowHelper.RegisterSubclass(hwnd, proc, id, refData);
    }

    /// <inheritdoc/>
    public bool RemoveSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id)
    {
        return Win32WindowHelper.RemoveSubclass(hwnd, proc, id);
    }

    /// <inheritdoc/>
    public bool UnregisterHotkey(IntPtr hwnd, int id)
    {
        return Win32WindowHelper.UnregisterHotkey(hwnd, id);
    }
}
