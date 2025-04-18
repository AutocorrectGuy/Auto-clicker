using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

public class GlobalHotkeys
{
  // Windows message ID for global hotkeys.
  // Sent to the window when a registered hotkey is pressed.
  public const int WM_HOTKEY = 0x0312;

  [DllImport("user32.dll")]
  private static extern bool RegisterHotKey(
      IntPtr hWnd,
      int id,
      uint fsModifiers,
      uint vk);

  [DllImport("user32.dll")]
  private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

  private HwndSource _source;
  private IntPtr _hWnd = IntPtr.Zero;
  private int _nextId = 1;

  private readonly Dictionary<int, Action> _hotkeyActions = new Dictionary<int, Action>();

  public void Initialize(Window window)
  {
    // Get the native HWND handle
    var helper = new WindowInteropHelper(window);
    _hWnd = helper.Handle;

    if (_hWnd == IntPtr.Zero)
      throw new InvalidOperationException("Window handle is not ready (HWND == 0). Call Initialize from OnSourceInitialized.");

    // Hook into the message loop
    _source = HwndSource.FromHwnd(_hWnd);
    _source.AddHook(HwndHook);

    // Unregister all hotkeys when the window closes
    window.Closing += OnWindowClose;
  }

  private void OnWindowClose(object sender, CancelEventArgs e)
  {
    foreach (var id in _hotkeyActions.Keys)
      UnregisterHotKey(_hWnd, id);

    if (_source != null)
      _source.RemoveHook(HwndHook);
  }

  public void RegisterHotkey(Key key, ModifierKeys modifiers, Action callback)
  {
    if (_hWnd == IntPtr.Zero)
      throw new InvalidOperationException("HotkeyService is not initialized. Call Initialize(window) first.");

    int id = _nextId++;
    _hotkeyActions[id] = callback;

    bool success = RegisterHotKey(_hWnd, id, (uint)modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));

    if (!success)
      throw new InvalidOperationException(string.Format("Failed to register hotkey: {0} + {1}", modifiers, key));
  }

  public IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
  {
    if (msg == WM_HOTKEY)
    {
      Action action;
      if (_hotkeyActions.TryGetValue(wParam.ToInt32(), out action))
      {
        action.Invoke();
        handled = true;
      }
    }

    return IntPtr.Zero;
  }
}