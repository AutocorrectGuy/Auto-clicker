using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

//
// - Used windows messages in this application
//
public enum WindowsMessageID {
    WM_MOUSEMOVE = 0x0200, // capturing mouse movement
    WM_LBUTTONDOWN = 0x0201, // left mouse down
    WM_LBUTTONUP = 0x0202, // left mouse up
    WM_RBUTTONDOWN = 0x0204, // right mouse down
    WM_RBUTTONUP = 0x0205, // right mouse up
    WM_HOTKEY = 0x0312, // app-wise global hotkeys
}


//
// - Extended System.Windows.Input.ModifierKeys, because
// they did not had NoRepeat option
//
[Flags]
public enum ExtendedModifierKeys {
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}

public static class User32 {
    //
    // structs
    //

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    // Interop structures and definitions
    public const uint INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    // Virtual keys
    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;
    public const int WM_MOUSEWHEEL = 0x020A;

    // 
    // Register hotkeys
    //
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    //
    // Unregister hotkeys
    //
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    //
    // Send inputs (e.g. mouse clicks, wheel scrolls)
    //
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    //
    // Read cursor position
    // 

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    // 
    // Reading users input
    //
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    //
    // Set cursors position
    //

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);


    /// 
    /// Retrieves the native window handle (HWND) from a WPF Window.
    /// 
    public static IntPtr GetHWnd(Window window) {
        WindowInteropHelper wHelper = new WindowInteropHelper(window);
        IntPtr hwnd = wHelper.Handle;

        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Window handle is not ready (HWND == 0). Call from OnSourceInitialized.");

        return hwnd;
    }

    /// 
    /// Retrieves the HwndSource for the given HWND.
    /// 
    public static HwndSource GetHWndSource(IntPtr hwnd) {
        return HwndSource.FromHwnd(hwnd);
    }



    public static void SendMouseDown(bool left) {
        INPUT input = new INPUT {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT {
                dwFlags = left ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN
            }
        };

        SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void SendMouseUp(bool left) {
        INPUT input = new INPUT {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT {
                dwFlags = left ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP
            }
        };

        SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }
}