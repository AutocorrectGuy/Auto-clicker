using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

public class WindowsMessageBinder
{

  public bool Initialized = false;

  public HotkeyHandler HotkeyHandler;

  private HWndHookHandler _hWndHookHandler;

  public MouseInputTracker MouseInputTracker;

  private IntPtr _hWnd;

  public WindowsMessageBinder()
  {
    MouseInputTracker = new MouseInputTracker();
  }

  public void Initialize(Window window, Action registerHotkeys)
  {
    _hWndHookHandler = new HWndHookHandler(window);
    _hWnd = _hWndHookHandler.HWnd;

    HotkeyHandler = new HotkeyHandler(_hWnd);

    registerHotkeys();
    RegisterAllMessageTypes();
    Initialized = true;
  }

  public void RegisterAllMessageTypes()
  {
    // register hotkey handler
    _hWndHookHandler.RegisterMessageHandler(WindowsMessageID.WM_HOTKEY, HotkeyHandler.HandleHotkeyMessages);
  }

}

