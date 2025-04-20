using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;


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

public class SnapshotFileList
{
  private string _path;

  public string[] Paths { get; set; }
  public string[] Names { get; set; }

  public SnapshotFileList(string path)
  {
    _path = path;
    Load();
  }

  public void Load()
  {
    var files = new DirectoryInfo(_path)
        .GetFiles("*.csv")
        .OrderByDescending(f => f.CreationTime)
        .ToArray();

    string[] paths = files.Select(f => f.FullName).ToArray();
    string[] names = files.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToArray();

    Paths = paths;
    Names = names;
  }
}


public class RelayCommand : ICommand
{
  private Action _execute;
  private Func<bool> _canExecute;

  public event EventHandler CanExecuteChanged
  {
    add { CommandManager.RequerySuggested += value; }
    remove { CommandManager.RequerySuggested -= value; }
  }

  public RelayCommand(Action execute, Func<bool> canExecute = null)
  {
    _execute = execute;
    _canExecute = canExecute ?? (() => true);
  }

  public void Execute(object param = null)
  {
    _execute();
  }

  public bool CanExecute(object param = null)
  {
    return _canExecute();
  }
}


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


public class MouseInputSnapshot
{
  public int X { get; set; }
  public int Y { get; set; }
  public bool IsLeftButtonDown { get; set; }
  public bool IsRightButtonDown { get; set; }
  public double Timestamp { get; set; }
}

public class MouseInputTracker
{
  private readonly List<MouseInputSnapshot> _snapshots = new List<MouseInputSnapshot>();
  private int _intervalMs = 25;

  public bool IsTracking = false;
  public Action<bool> IsTrackingAction;

  public bool IsPlaying = false;
  public Action<bool> IsPlayingAction;

  public bool IsLooping = false;

  public double PlaybackSpeed = 1.0;

  public MouseInputTracker()
  {
    IsPlayingAction = (bool status) => { IsPlaying = status; };
    IsTrackingAction = (bool status) => { IsTracking = status; };
  }

  public void StartTracking()
  {
    if (IsTracking || IsPlaying) return;

    IsTrackingAction.Invoke(true);

    Task.Run(() =>
    {
      Stopwatch stopwatch = Stopwatch.StartNew();
      long nextTick = _intervalMs;

      while (IsTracking)
      {
        long elapsedMs = stopwatch.ElapsedMilliseconds;

        if (elapsedMs >= nextTick)
        {
          _snapshots.Add(CreateSnapshot((int)elapsedMs));
          nextTick += _intervalMs;
        }

        Thread.Sleep(1);
      }
    });
  }

  // Can be invoked both b
  public void StopTracking()
  {
    if (!IsTracking) return;

    IsTracking = false;

    WriteOutputFile();
    _snapshots.Clear();
    IsTrackingAction.Invoke(false); // trigger ui after file list refreshed
  }

  private MouseInputSnapshot CreateSnapshot(int relativeTimestamp)
  {
    User32.POINT pt;
    User32.GetCursorPos(out pt);

    return new MouseInputSnapshot
    {
      X = pt.X,
      Y = pt.Y,
      IsLeftButtonDown = (User32.GetAsyncKeyState(User32.VK_LBUTTON) & 0x8000) != 0,
      IsRightButtonDown = (User32.GetAsyncKeyState(User32.VK_RBUTTON) & 0x8000) != 0,
      Timestamp = relativeTimestamp
    };
  }

  public void WriteOutputFile()
  {
    string defaultFileName = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";

    SaveFileDialog saveDialog = new SaveFileDialog
    {
      FileName = defaultFileName,
      Filter = "CSV Files (*.csv)|*.csv",
      DefaultExt = ".csv",
      Title = "Save Snapshot As"
    };

    bool? result = saveDialog.ShowDialog();

    if (result == true)
    {
      string path = saveDialog.FileName;

      using (StreamWriter outputFile = new StreamWriter(path))
      {
        foreach (MouseInputSnapshot sn in _snapshots)
        {
          string csvLine = String.Concat(sn.X, ",", sn.Y, ",", sn.IsLeftButtonDown ? 1 : 0, ",", sn.IsRightButtonDown ? 1 : 0, ",", sn.Timestamp);
          outputFile.WriteLine(csvLine);
        }
      }
    }
  }

  public void PlaySnapshotFromFile(string filePath)
  {
    if (IsPlaying || IsTracking) return;

    IsPlayingAction.Invoke(true);

    if (!File.Exists(filePath))
    {
      MessageBox.Show("File does not exist: " + filePath);
      return;
    }

    List<MouseInputSnapshot> loadedSnapshots = new List<MouseInputSnapshot>();

    foreach (var line in File.ReadAllLines(filePath))
    {
      var parts = line.Split(',');
      if (parts.Length != 5) continue;

      loadedSnapshots.Add(new MouseInputSnapshot
      {
        X = int.Parse(parts[0]),
        Y = int.Parse(parts[1]),
        IsLeftButtonDown = parts[2] == "1",
        IsRightButtonDown = parts[3] == "1",
        Timestamp = int.Parse(parts[4]) * PlaybackSpeed
      });
    }

    Task.Run(() =>
    {
      if (IsLooping)
        while (IsLooping) _playShapshotOnce(loadedSnapshots);
      else
        _playShapshotOnce(loadedSnapshots);

      IsPlayingAction.Invoke(false);
    });
  }

  private void _playShapshotOnce(List<MouseInputSnapshot> loadedSnapshots)
  {

    Stopwatch stopwatch = Stopwatch.StartNew();

    int current = 0;
    bool prevLeftDown = false; // left mouse down
    bool prevRightDown = false; // right mouse down

    while (IsPlaying && current < loadedSnapshots.Count)
    {
      int elapsed = (int)stopwatch.ElapsedMilliseconds;
      MouseInputSnapshot snap = loadedSnapshots[current];

      if (elapsed >= snap.Timestamp)
      {
        User32.SetCursorPos(snap.X, snap.Y);

        // Detect left click transition
        if (snap.IsLeftButtonDown && !prevLeftDown)
          User32.SendMouseDown(true); // left down
        else if (!snap.IsLeftButtonDown && prevLeftDown)
          User32.SendMouseUp(true); // left up

        // Detect right click transition
        if (snap.IsRightButtonDown && !prevRightDown)
          User32.SendMouseDown(false); // right down
        else if (!snap.IsRightButtonDown && prevRightDown)
          User32.SendMouseUp(false); // right up

        // Update previous states
        prevLeftDown = snap.IsLeftButtonDown;
        prevRightDown = snap.IsRightButtonDown;

        current++;
      }

      Thread.Sleep(1);
    }

    // release if still down at end
    if (prevLeftDown) User32.SendMouseUp(true);
    if (prevRightDown) User32.SendMouseUp(true);
  }
}

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

public class MainWindowVM : ViewModelBase
{
    //
    // initialized in OnSourceInitialized in MainWindow.OnSourceInitialized
    // 
    public WindowsMessageBinder WindowsMessageBinder;

    private readonly string _snapshotsPath = "C:/Users/marti/Desktop/snapshots";

    private SnapshotFileList _snapshotFileList;
    public SnapshotFileList SnapshotFileList
    {
        get { return _snapshotFileList; }
        set { _snapshotFileList = value; OnPropertyChanged(); }
    }

    private int _selectedSnapshotIndex;
    public int SelectedSnapshotIndex
    {
        get { return _selectedSnapshotIndex; }
        set { _selectedSnapshotIndex = value; OnPropertyChanged(); }
    }

    public bool IsTracking { get; set; }

    public bool IsPlaying { get; set; }

    public bool IsLooping
    {
        get { return WindowsMessageBinder.MouseInputTracker.IsLooping; }
        set { WindowsMessageBinder.MouseInputTracker.IsLooping = value; OnPropertyChanged(); }
    }

    // Playback speed slider
    public double PlaybackSpeed { get { return _playbackSpeedOptions[PlaybackSpeedIndex]; } }
    private readonly double[] _playbackSpeedOptions = new double[] { 0.1, 0.5, 1, 2, 5, 20 };
    private int _playbackSpeedIndex = 2;
    public int PlaybackSpeedIndex
    {
        get { return _playbackSpeedIndex; }
        set
        {
            if (_playbackSpeedIndex != value)
            {
                _playbackSpeedIndex = value;
                // update source playback speed value
                WindowsMessageBinder.MouseInputTracker.PlaybackSpeed = 1 / _playbackSpeedOptions[PlaybackSpeedIndex];
                OnPropertyChanged("PlaybackSpeed");
            }
        }
    }

    private RelayCommand _runSnapshot;
    public RelayCommand RunSnapshot
    {
        get
        {
            if (_runSnapshot == null)
                _runSnapshot = new RelayCommand(_runShapshot);
            return _runSnapshot;
        }
    }

    public MainWindowVM()
    {
        SnapshotFileList = new SnapshotFileList(_snapshotsPath);
        WindowsMessageBinder = new WindowsMessageBinder();

        WindowsMessageBinder.MouseInputTracker.IsPlayingAction += (status) =>
        {
            IsPlaying = status;
            OnPropertyChanged("IsPlaying");
        };
        WindowsMessageBinder.MouseInputTracker.IsTrackingAction += (status) =>
        {
            IsTracking = status;
            OnPropertyChanged("IsTracking");
            // after tracking reload the file list
            SnapshotFileList = new SnapshotFileList(_snapshotsPath);
        };
        
        IsTracking = false;
        IsPlaying = false;
    }

    protected void _runShapshot()
    {
        string snaphotPath = _snapshotFileList.Paths[SelectedSnapshotIndex];

        if (string.IsNullOrEmpty(snaphotPath))
        {
            MessageBox.Show(string.Concat("Invalid file path: ", snaphotPath));
            return;
        }

        OnPropertyChanged("IsPlayingStatusText");
        WindowsMessageBinder.MouseInputTracker.PlaySnapshotFromFile(snaphotPath);
    }
}

public class MainWindowVMWithHotKeys : MainWindowVM
{

    public void RegisterHotkeys()
    {
        if (WindowsMessageBinder == null) return;

        // Escape
        WindowsMessageBinder.HotkeyHandler.RegisterHotkey(
            Key.Escape,
            ExtendedModifierKeys.None,
            _handleEscapeKey);

        // Spacebar
        WindowsMessageBinder.HotkeyHandler.RegisterHotkey(
            Key.Space,
            ExtendedModifierKeys.None,
            _handleSpaceKey);

        // Ctrl + R
        WindowsMessageBinder.HotkeyHandler.RegisterHotkey(
            Key.R,
            ExtendedModifierKeys.Control | ExtendedModifierKeys.NoRepeat,
            _handleCtrlRKey);
    }

    //
    // -- Key handlers --
    //

    //
    // ESCAPE key handler
    //
    private void _handleEscapeKey()
    {
        // stop tracking if tracking
        if (WindowsMessageBinder.MouseInputTracker.IsTracking)
        {
            if (WindowsMessageBinder != null)
                WindowsMessageBinder.MouseInputTracker.StopTracking();
            OnPropertyChanged("IsTrackingStatusText");
        }

        // stop playing if playing
        if (WindowsMessageBinder.MouseInputTracker.IsPlaying)
        {
            if (WindowsMessageBinder != null)
                WindowsMessageBinder.MouseInputTracker.IsPlayingAction.Invoke(false);
            OnPropertyChanged("IsPlayingStatusText");
        }
    }


    //
    // SPACEBAR key handler
    //
    private void _handleSpaceKey()
    {
        // start tracking
        if (WindowsMessageBinder != null)
            WindowsMessageBinder.MouseInputTracker.StartTracking();
        OnPropertyChanged("IsTrackingStatusText");
    }

    //
    // R key handler
    //
    private void _handleCtrlRKey()
    {
        _runShapshot();
    }
}


public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string name = "")
    {
        if (PropertyChanged == null) return;
        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
namespace AutoClickerApp
{
  public class MainWindow : Window
  {
    private readonly MainWindowVMWithHotKeys _viewModel;

    public MainWindow()
    {
      _viewModel = new MainWindowVMWithHotKeys();
      DataContext = _viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);

      _viewModel.WindowsMessageBinder.Initialize(this, _viewModel.RegisterHotkeys);
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {

      _viewModel.WindowsMessageBinder.HotkeyHandler.UnregisterHotkeys();
    }
  }
}

