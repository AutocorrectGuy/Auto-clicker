using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

public class MainWindowVM : ViewModelBase
{
    //
    // initialized in OnSourceInitialized in MainWindow.OnSourceInitialized
    // 
    public WindowsMessageBinder WindowsMessageBinder;

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
        IsTracking = false;
        IsPlaying = false;

        WindowsMessageBinder = new WindowsMessageBinder();
        SnapshotFileList = new SnapshotFileList(WindowsMessageBinder.MouseInputTracker.SnapshotsPath);

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
            SnapshotFileList = new SnapshotFileList(WindowsMessageBinder.MouseInputTracker.SnapshotsPath);
        };
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
