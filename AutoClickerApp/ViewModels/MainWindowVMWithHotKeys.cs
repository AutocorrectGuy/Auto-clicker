using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

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

