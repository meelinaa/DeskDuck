using DeskDuck.Core.Helper;

namespace DeskDuck.Core.Features.Shell;

/// <summary>
/// Abstraction for interacting with the underlying OS windowing system,
/// enabling easier unit testing of ViewModels without direct Win32 API calls.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Configures the specified window handle with overlay styles.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    void ConfigureOverlayStyles(IntPtr hwnd);

    /// <summary>
    /// Registers a global hotkey for the specified window.
    /// </summary>
    /// <param name="hwnd">The window handle to receive the hotkey messages.</param>
    /// <param name="id">An identifier for the hotkey.</param>
    /// <param name="modifiers">Key modifiers (e.g., ALT, CTRL).</param>
    /// <param name="vk">The virtual key code.</param>
    /// <returns>True if registration succeeded; otherwise, false.</returns>
    bool RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint vk);

    /// <summary>
    /// Unregisters a previously registered global hotkey.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <param name="id">The identifier of the hotkey to unregister.</param>
    /// <returns>True if unregistration succeeded; otherwise, false.</returns>
    bool UnregisterHotkey(IntPtr hwnd, int id);

    /// <summary>
    /// Registers a subclass procedure to intercept window messages.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <param name="proc">The subclass procedure callback.</param>
    /// <param name="id">An identifier for the subclass.</param>
    /// <param name="refData">Reference data passed to the callback.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    bool RegisterSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id, IntPtr refData);

    /// <summary>
    /// Removes a subclass procedure from the specified window.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <param name="proc">The subclass procedure to remove.</param>
    /// <param name="id">The identifier of the subclass.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    bool RemoveSubclass(IntPtr hwnd, Win32WindowHelper.SUBCLASSPROC proc, IntPtr id);

    /// <summary>
    /// Calls the default subclass procedure for unhandled window messages.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <param name="msg">The window message identifier.</param>
    /// <param name="wParam">Additional message information.</param>
    /// <param name="lParam">Additional message information.</param>
    /// <returns>The result of message processing.</returns>
    IntPtr DefaultSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Retrieves the current screen coordinates of the mouse cursor.
    /// </summary>
    /// <param name="point">Outputs the cursor position.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    bool GetCursorPosition(out Win32WindowHelper.PointStruct point);

    /// <summary>Gets the default hotkey identifier.</summary>
    int HotkeyId { get; }

    /// <summary>Gets the CTRL modifier value.</summary>
    uint ModControl { get; }

    /// <summary>Gets the ALT modifier value.</summary>
    uint ModAlt { get; }

    /// <summary>Gets the SHIFT modifier value.</summary>
    uint ModShift { get; }

    /// <summary>Gets the virtual key code for 'D'.</summary>
    uint VkD { get; }

    /// <summary>Gets the window message identifier for a hotkey event.</summary>
    uint WmHotkey { get; }
}
