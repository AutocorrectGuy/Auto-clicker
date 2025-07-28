using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

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
  // ---------- CONSTANTS ----------
  private const int _intervalMs = 10;   // snapshot interval
  private const int _minClickMs = 30;  // minimal press length (0.03 s)

  // ---------- PUBLIC STATE ----------
  public string SnapshotsPath = Path.GetFullPath("./Snapshots");
  public bool IsTracking = false;
  public bool IsPlaying = false;
  public bool IsLooping = false;
  public bool isQuickSave = false;
  public double PlaybackSpeed = 1.0;  // 1 = realtime, >1 = faster

  // UI callbacks
  public Action<bool> IsTrackingAction;
  public Action<bool> IsPlayingAction;

  // ---------- PRIVATE ----------
  private readonly List<MouseInputSnapshot> _snapshots = new List<MouseInputSnapshot>();
  private readonly object _stateLock = new object();
  private Thread _trackingThread;
  private Thread _playThread;

  // ---------- CONSTRUCTOR ----------
  public MouseInputTracker()
  {
    _initSnapshotsPath();
    IsTrackingAction = delegate (bool _) { };
    IsPlayingAction = delegate (bool _) { };
  }

  // ==========================================================
  //  RECORDING
  // ==========================================================
  public void StartTracking()
  {
    lock (_stateLock)
    {
      if (IsTracking || IsPlaying) return;
      IsTracking = true;
    }

    _trackingThread = new Thread(TrackingLoop) { IsBackground = true };
    _trackingThread.Start();
    IsTrackingAction(true);
  }

  private void TrackingLoop()
  {
    Stopwatch sw = Stopwatch.StartNew();
    long nextTick = _intervalMs;

    while (true)
    {
      lock (_stateLock) { if (!IsTracking) break; }

      long elapsed = sw.ElapsedMilliseconds;
      if (elapsed >= nextTick)
      {
        _snapshots.Add(CreateSnapshot((int)elapsed));
        nextTick += _intervalMs;
      }
      Thread.Sleep(1);
    }
  }

  public bool StopTracking()
  {
    lock (_stateLock)
    {
      if (!IsTracking) return false;
      IsTracking = false;
    }

    if (_trackingThread != null && _trackingThread.IsAlive)
      _trackingThread.Join();

    WriteOutputFile();
    _snapshots.Clear();
    IsTrackingAction(false);
    return true;
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

  // ==========================================================
  //  FILE I/O
  // ==========================================================
  public void WriteOutputFile()
  {
    string defaultName = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
    string path;

    if (isQuickSave)
    {
      path = Path.Combine(SnapshotsPath, defaultName);
    }
    else
    {
      SaveFileDialog dlg = new SaveFileDialog
      {
        FileName = defaultName,
        Filter = "CSV Files (*.csv)|*.csv",
        DefaultExt = ".csv",
        Title = "Save Snapshot As",
        InitialDirectory = SnapshotsPath
      };
      if (dlg.ShowDialog() != true) return;
      path = dlg.FileName;
    }

    using (StreamWriter w = new StreamWriter(path))
    {
      foreach (MouseInputSnapshot s in _snapshots)
        w.WriteLine(String.Concat(
            s.X, ",",
            s.Y, ",",
            s.IsLeftButtonDown ? 1 : 0, ",",
            s.IsRightButtonDown ? 1 : 0, ",",
            s.Timestamp));
    }
  }

  // ==========================================================
  //  PLAYBACK
  // ==========================================================
  public void PlaySnapshotFromFile(string filePath)
  {
    /* ----------- UPDATED BLOCK ----------- */
    lock (_stateLock)
    {
      if (IsTracking) return; // don’t start during recording

      if (_playThread != null && _playThread.IsAlive)
        _playThread.Join(); // wait for any previous playback

      if (IsPlaying) return; // still playing – bail

      IsPlaying = true; // mark new playback
    }
    /* ----------- END UPDATED BLOCK -------- */

    if (!File.Exists(filePath))
    {
      IsPlaying = false;
      MessageBox.Show("File does not exist: " + filePath);
      return;
    }

    List<MouseInputSnapshot> snaps = _loadSnapshots(filePath);
    IsPlayingAction(true);

    _playThread = new Thread(delegate ()
    {
      try
      {
        do { _playOnce(snaps); } while (IsLooping);
      }
      finally
      {
        lock (_stateLock) { IsPlaying = false; }
        IsPlayingAction(false);
      }
    })
    { IsBackground = true };
    _playThread.Start();
  }

  private List<MouseInputSnapshot> _loadSnapshots(string filePath)
  {
    List<MouseInputSnapshot> list = new List<MouseInputSnapshot>();

    foreach (string line in File.ReadAllLines(filePath))
    {
      string[] p = line.Split(',');
      if (p.Length != 5) continue;

      double ts = Convert.ToInt32(p[4]) * PlaybackSpeed;   // 0.1 → 10× slower,  2 → 2× faster

      list.Add(new MouseInputSnapshot
      {
        X = Convert.ToInt32(p[0]),
        Y = Convert.ToInt32(p[1]),
        IsLeftButtonDown = p[2] == "1",
        IsRightButtonDown = p[3] == "1",
        Timestamp = ts
      });
    }

    list = _pruneSnapshotsForSpeed(list);
    _ensureMinHold(list, delegate (MouseInputSnapshot s) { return s.IsLeftButtonDown; });
    _ensureMinHold(list, delegate (MouseInputSnapshot s) { return s.IsRightButtonDown; });

    return list;
  }

  private List<MouseInputSnapshot> _pruneSnapshotsForSpeed(List<MouseInputSnapshot> sn)
  {
    if (PlaybackSpeed >= 1.0) return sn;

    int factor = (int)Math.Max(2, Math.Round(1.0 / PlaybackSpeed));
    List<MouseInputSnapshot> pruned = new List<MouseInputSnapshot>(sn.Count / factor + 8);

    bool prevL = false, prevR = false;
    int index = 0;

    foreach (MouseInputSnapshot s in sn)
    {
      bool stateChange = (s.IsLeftButtonDown != prevL) || (s.IsRightButtonDown != prevR);

      if (stateChange || (index % factor == 0))
        pruned.Add(s);

      prevL = s.IsLeftButtonDown;
      prevR = s.IsRightButtonDown;
      index++;
    }
    return pruned;
  }

  private void _ensureMinHold(List<MouseInputSnapshot> sn, Predicate<MouseInputSnapshot> isDown)
  {
    int lastDown = -1;

    for (int i = 0; i < sn.Count; i++)
    {
      if (isDown(sn[i]) && (i == 0 || !isDown(sn[i - 1])))
        lastDown = i;
      else if (!isDown(sn[i]) && i > 0 && isDown(sn[i - 1]))
      {
        double hold = sn[i].Timestamp - sn[lastDown].Timestamp;
        if (hold < _minClickMs)
        {
          double delta = _minClickMs - hold;
          for (int j = i; j < sn.Count; j++)
            sn[j].Timestamp += delta;
        }
      }
    }
  }

  private void _playOnce(List<MouseInputSnapshot> sn)
  {
    Stopwatch sw = Stopwatch.StartNew();
    int cur = 0;
    bool prevL = false, prevR = false;
    long lastLDown = -1, lastRDown = -1;

    while (true)
    {
      /* ----- HARD‑STOP ON ESCAPE (UPDATED) ----- */
      if ((User32.GetAsyncKeyState(0x1B) & 0x8000) != 0)   // VK_ESCAPE
      {
        lock (_stateLock)
        {
          IsPlaying = false;  // stop current loop
          IsLooping = false;  // prevent outer loop restart
        }
      }

      bool playing;
      lock (_stateLock) { playing = IsPlaying; }
      if (!playing || cur >= sn.Count) break;

      int elapsed = (int)sw.ElapsedMilliseconds;
      MouseInputSnapshot s = sn[cur];

      if (elapsed >= s.Timestamp)
      {
        User32.SetCursorPos(s.X, s.Y);

        // ------- LEFT -------
        if (s.IsLeftButtonDown && !prevL)
        {
          User32.SendMouseDown(true);
          lastLDown = elapsed;
        }
        else if (!s.IsLeftButtonDown && prevL)
        {
          int hold = elapsed - (int)lastLDown;
          if (hold < _minClickMs) Thread.Sleep(_minClickMs - hold);
          User32.SendMouseUp(true);
        }

        // ------- RIGHT -------
        if (s.IsRightButtonDown && !prevR)
        {
          User32.SendMouseDown(false);
          lastRDown = elapsed;
        }
        else if (!s.IsRightButtonDown && prevR)
        {
          int hold = elapsed - (int)lastRDown;
          if (hold < _minClickMs) Thread.Sleep(_minClickMs - hold);
          User32.SendMouseUp(false);
        }

        prevL = s.IsLeftButtonDown;
        prevR = s.IsRightButtonDown;
        cur++;
      }
      Thread.Sleep(1);
    }

    if (prevL) User32.SendMouseUp(true);
    if (prevR) User32.SendMouseUp(false);
  }

  // ==========================================================
  //  MISC
  // ==========================================================
  private void _initSnapshotsPath()
  {
    if (!Directory.Exists(SnapshotsPath))
      Directory.CreateDirectory(SnapshotsPath);
  }
}
