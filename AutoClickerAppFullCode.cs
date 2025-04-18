using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Threading;
using System.Threading.Tasks;


namespace AutoClickerApp
{
    public class App : System.Windows.Application
    {
        private string _XAML_PATH = "./AutoClickerApp/Views/MainWindow.xaml";

        public App()
        {
            try
            {
                MainWindow = (MainWindow)XamlReader.Parse(File.ReadAllText(_XAML_PATH));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                    Console.WriteLine("Stack Trace: " + ex.InnerException.StackTrace);
                }
            }
        }

        public void Start()
        {
            Run(MainWindow);
        }
    }
}


public static class GlobalHotkeyConfig
{
  public static void Setup(
    GlobalHotkeys hotkeyService,
    AutoClicker autoClicker,
    Window window)
  {
    hotkeyService.Initialize(window);

    // VK_SPACE: start clicking 
    hotkeyService.RegisterHotkey(
      Key.Space,
      ModifierKeys.None | (ModifierKeys)0x4000,
      autoClicker.StartClicking);

    // VK_ESCAPE: stop clicking 
    hotkeyService.RegisterHotkey(
      Key.Escape,
      ModifierKeys.None | (ModifierKeys)0x4000,
      autoClicker.StopClicking);
  }
}

public class AutoClicker : ViewModelBase
{
  private bool _isClicking;
  public bool IsClicking
  {
    get { return _isClicking; }
    set
    {
      _isClicking = value;
      OnPropertyChanged(); // update bool data trigger in ui
      OnPropertyChanged("IsClickingStatusText"); // update text field value in ui
    }
  }

  private int _intervalMs;
  public int IntervalMs
  {
    get { return _intervalMs; }
    private set
    {
      _intervalMs = value;
      OnPropertyChanged(); // update the clicks count number in ui
    }
  }

  private int _clicksCount;
  public int ClicksCount
  {
    get { return _clicksCount; }
    set
    {
      _clicksCount = value;
      OnPropertyChanged();
    }
  }

  public string IsClickingStatusText
  {
    get { return _isClicking ? "Clicking" : "Not clicking"; }
  }

  // CPS input field (binds to user TextBox)
  private string _cpsInput = "40";
  public string CpsInput
  {
    get { return _cpsInput; }
    set
    {
      _cpsInput = value;
      OnPropertyChanged(); // update inputfield value in ui
      UpdateIntervalFromCps(value); // update interval value in ui
    }
  }

  private void UpdateIntervalFromCps(string input)
  {
    double cps;
    if (double.TryParse(
      input,
      System.Globalization.NumberStyles.Float,
      System.Globalization.CultureInfo.InvariantCulture,
      out cps))
    {
      // 1 to 1000 cps
      if (cps >= 0.1 && cps <= 1000)
        IntervalMs = (int)(1000.0 / cps);
    }
  }

  private readonly UserInputHandler _userInputHandler;

  public AutoClicker(UserInputHandler userInputHandler)
  {
    _userInputHandler = userInputHandler;
    _isClicking = false;
    _clicksCount = 0;
    _intervalMs = 25; // default ~40 CPS (1000 ms / 25 ms = 40 cps)
  }

  public void StartClicking()
  {
    // prevent overlapping loops and using more than 1 separate thread 
    if (_isClicking)
      return;

    IsClicking = true;

    // run task on separate thread, so the app does not freeze or bug out
    Task.Run(() =>
    {
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      long intervalTicks = TimeSpan.FromMilliseconds(_intervalMs).Ticks;
      long nextTick = stopwatch.ElapsedTicks;

      // some delt time stuff - better than just using Thread.Sleep(ms)
      while (_isClicking)
      {
        long now = stopwatch.ElapsedTicks;

        if (now >= nextTick)
        {
          _userInputHandler.SendLeftClick();
          nextTick += intervalTicks;
          ClicksCount++;
        }

        Thread.Sleep(1); // small sleep to avoid CPU burn
      }

      stopwatch.Stop();
    });
  }


  public void StopClicking()
  {
    IsClicking = false;
  }
}

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

public class UserInputHandler
{
  public void SendLeftClick()
  {
    var inputs = new INPUT[2];

    // Mouse down
    inputs[0] = new INPUT
    {
      type = INPUT_MOUSE,
      u = new InputUnion
      {
        mi = new MOUSEINPUT
        {
          dwFlags = MOUSEEVENTF_LEFTDOWN
        }
      }
    };

    // Mouse up
    inputs[1] = new INPUT
    {
      type = INPUT_MOUSE,
      u = new InputUnion
      {
        mi = new MOUSEINPUT
        {
          dwFlags = MOUSEEVENTF_LEFTUP
        }
      }
    };

    SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
  }

  // Interop structures and definitions

  private const uint INPUT_MOUSE = 0;
  private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  private const uint MOUSEEVENTF_LEFTUP = 0x0004;

  [DllImport("user32.dll", SetLastError = true)]
  private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

  [StructLayout(LayoutKind.Sequential)]
  private struct INPUT
  {
    public uint type;
    public InputUnion u;
  }

  [StructLayout(LayoutKind.Explicit)]
  private struct InputUnion
  {
    [FieldOffset(0)] public MOUSEINPUT mi;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct MOUSEINPUT
  {
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
  }
}

public class MainWindowVM : ViewModelBase
{
  private readonly Window _window;

  public readonly GlobalHotkeys GlobalHotkeys;

  public AutoClicker AutoClicker { get; set; }
  
  private readonly UserInputHandler _userInputHandler;

  public MainWindowVM(Window window)
  {
    _window = window;
    _userInputHandler = new UserInputHandler();
    GlobalHotkeys = new GlobalHotkeys();
    AutoClicker = new AutoClicker(_userInputHandler);
  }
}

public class ViewModelBase : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler PropertyChanged;

  public void OnPropertyChanged([CallerMemberName] string name = null)
  {
    if (PropertyChanged == null) return;
    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
  }
}
namespace AutoClickerApp
{
  public class MainWindow : Window
  {
    private MainWindowVM _mainWindowVM;

    public MainWindow()
    {
      _mainWindowVM = new MainWindowVM(this);
      DataContext = _mainWindowVM;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);
      GlobalHotkeyConfig.Setup(
        _mainWindowVM.GlobalHotkeys,
        _mainWindowVM.AutoClicker,
        this);
    }
  }
}

