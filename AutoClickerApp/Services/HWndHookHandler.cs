using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;

public delegate void MessageCallback(
    IntPtr hwnd,
    WindowsMessageID msg,
    IntPtr wParam,
    IntPtr lParam,
    ref bool handled);

/// <summary>
/// Handles hooking into the WPF window message loop and routing Windows messages to registered handlers.
/// </summary>
public class HWndHookHandler
{
  /// <summary>
  /// Native window handle (HWND).
  /// </summary>
  public readonly IntPtr HWnd;

  /// <summary>
  /// Delegate signature for Windows message handlers.
  /// </summary>
  public delegate void MessageHandler(
      IntPtr hwnd,
      WindowsMessageID msg,
      IntPtr wParam,
      IntPtr lParam,
      ref bool handled);

  /// <summary>
  /// Type used to validate enum entries from Windows message integers.
  /// </summary>
  private static readonly Type WindowsMessageID_TYPE = typeof(WindowsMessageID);

  /// <summary>
  /// Dictionary of registered message handlers, keyed by WindowsMessageID enum.
  /// </summary>
  private readonly Dictionary<WindowsMessageID, MessageHandler> _wmCallbacks;

  /// <summary>
  /// Source of the window's message loop.
  /// </summary>
  private readonly HwndSource _hWndSrc;

  /// <summary>
  /// Constructor. Initializes the HWND hook system and sets default handlers.
  /// </summary>
  /// <param name="window">The main application window.</param>
  public HWndHookHandler(Window window)
  {
    HWnd = User32.GetHWnd(window);
    _hWndSrc = User32.GetHWndSource(HWnd);

    _wmCallbacks = new Dictionary<WindowsMessageID, MessageHandler>
            {
                { WindowsMessageID.WM_MOUSEMOVE, EmptyMessageHandler },
                { WindowsMessageID.WM_LBUTTONDOWN, EmptyMessageHandler },
                { WindowsMessageID.WM_LBUTTONUP, EmptyMessageHandler },
                { WindowsMessageID.WM_RBUTTONDOWN, EmptyMessageHandler },
                { WindowsMessageID.WM_RBUTTONUP, EmptyMessageHandler },
                { WindowsMessageID.WM_HOTKEY, EmptyMessageHandler }
            };

    _hWndSrc.AddHook(CustomHWndHook);
  }

  /// <summary>
  /// Windows message hook handler. Dispatches to the registered callback for that message ID.
  /// </summary>
  public IntPtr CustomHWndHook(
      IntPtr hwnd,
      int msg,
      IntPtr wParam,
      IntPtr lParam,
      ref bool handled)
  {

    // check if the message is something we would like to handle
    if (!Enum.IsDefined(WindowsMessageID_TYPE, msg))
      return IntPtr.Zero;

    // convert message id (int) to _WmCallbacks key
    WindowsMessageID wmID = (WindowsMessageID)msg;

    // run specified callback
    _wmCallbacks[wmID](hwnd, wmID, wParam, lParam, ref handled);

    return IntPtr.Zero;
  }

  /// <summary>
  /// Registers or overrides a message handler for the given WindowsMessageID.
  /// Throws an exception if the ID is not initially in the supported set.
  /// </summary>
  public void RegisterMessageHandler(WindowsMessageID id, MessageHandler handler)
  {
    if (!_wmCallbacks.ContainsKey(id))
    {
      string message = String.Format("Message ID '{0}' is not supported or not pre-initialized in _wmCallbacks.", id);
      throw new ArgumentException(message);
    }
    _wmCallbacks[id] = handler;
  }

  /// <summary>
  /// Default no-op handler used to initialize the callbacks dictionary.
  /// </summary>
  private void EmptyMessageHandler(
      IntPtr hwnd,
      WindowsMessageID msg,
      IntPtr wParam,
      IntPtr lParam,
      ref bool handled)
  {
    // No action. Dummy method that has to be replaced by calling `RegisterMessageHandler`
  }
}
