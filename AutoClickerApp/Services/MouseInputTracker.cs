using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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