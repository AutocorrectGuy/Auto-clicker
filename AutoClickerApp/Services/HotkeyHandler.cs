using System.Windows;
using System.Windows.Input;

/// <summary>
/// Handles global hotkey registration, message handling, and unregistration.
/// </summary>
public class HotkeyHandler
{
  /// <summary>
  /// Static counter for generating unique hotkey IDs.
  /// </summary>
  private static int _nextHotkeyId = 0;

  /// <summary>
  /// Handle to the window receiving hotkey messages.
  /// </summary>
  private readonly IntPtr _hWnd;

  /// <summary>
  /// Maps registered hotkey IDs to their corresponding callback actions.
  /// </summary>
  private readonly Dictionary<int, Action> _hotkeyActions = new Dictionary<int, Action>();

  /// <summary>
  /// Initializes the handler with a window handle used to register hotkeys.
  /// </summary>
  public HotkeyHandler(IntPtr hWnd)
  {
    _hWnd = hWnd;
  }

  /// <summary>
  /// Called from the main window’s message loop to handle WM_HOTKEY messages.
  /// </summary>
  public void HandleHotkeyMessages(
      IntPtr hwnd,                   // Not used here
      WindowsMessageID msg,          // Always WM_HOTKEY 0x0312 (not used here)
      IntPtr wParam,                 // Hotkey ID (as registered)
      IntPtr lParam,                 // Packed VK + modifiers (not used here)
      ref bool handled)
  {
    int hotkeyId = wParam.ToInt32();

    if (!_hotkeyActions.ContainsKey(hotkeyId))
      return;

    _hotkeyActions[hotkeyId]();
    handled = true;
  }

  /// <summary>
  /// Registers a global hotkey with a unique ID.
  /// </summary>
  public void RegisterHotkey(Key key, ExtendedModifierKeys modifiers, Action callback)
  {
    _nextHotkeyId++;

    bool success = User32.RegisterHotKey(
        _hWnd,
        _nextHotkeyId,
        (uint)modifiers,
        (uint)KeyInterop.VirtualKeyFromKey(key)
    );

    if (!success)
    {
      throw new InvalidOperationException(
          string.Format("Failed to register hotkey: {0} + {1}", modifiers, key));
    }

    _hotkeyActions[_nextHotkeyId] = callback;
  }

  /// <summary>
  /// Unregisters all previously registered hotkeys and clears the map.
  /// </summary>
  public void UnregisterHotkeys()
  {
    foreach (int hotkeyId in _hotkeyActions.Keys)
    {
      User32.UnregisterHotKey(_hWnd, hotkeyId);
    }

    _hotkeyActions.Clear();
  }
}
