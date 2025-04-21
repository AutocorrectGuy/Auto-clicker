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
            ExtendedModifierKeys.None | ExtendedModifierKeys.NoRepeat,
            _ESCAPE_handler);

        // Alt + C = quick save snapshot (automatically save)
        WindowsMessageBinder.HotkeyHandler.RegisterHotkey(
            Key.C,
            ExtendedModifierKeys.Alt | ExtendedModifierKeys.NoRepeat,
            _ALT_C_handler);

        // Alt + shift + C = save snaphot (after recording ask user for the file name)
        WindowsMessageBinder.HotkeyHandler.RegisterHotkey(
            Key.C,
            ExtendedModifierKeys.Alt | ExtendedModifierKeys.Shift | ExtendedModifierKeys.NoRepeat,
            _ALT_SHIFT_C_handler);


        // Alt + V run snapshot
        WindowsMessageBinder.HotkeyHandler.RegisterHotkey(
            Key.V,
            ExtendedModifierKeys.Alt | ExtendedModifierKeys.NoRepeat,
            _ALT_V_handler);

        // Alt + L Togle looping
        WindowsMessageBinder.HotkeyHandler.RegisterHotkey(
            Key.L,
            ExtendedModifierKeys.Alt | ExtendedModifierKeys.NoRepeat,
            _ALT_L_handler);
    }

    //
    // -- Key handlers --
    //

    //
    // [ESCAPE] Stop recording | stop running snapshot
    //
    private void _ESCAPE_handler()
    {
        // stop tracking if tracking
        if (WindowsMessageBinder.MouseInputTracker.IsTracking)
        {
            bool success = WindowsMessageBinder.MouseInputTracker.StopTracking();
            if (success) SelectedSnapshotIndex = 0; // select the last recoreded snapshot
            OnPropertyChanged("IsTrackingStatusText");
        }

        // stop playing if playing
        if (WindowsMessageBinder.MouseInputTracker.IsPlaying)
        {
            WindowsMessageBinder.MouseInputTracker.IsPlayingAction.Invoke(false);
            OnPropertyChanged("IsPlayingStatusText");
        }
    }

    //
    // [Alt + C] Recording with quick save
    //
    private void _ALT_C_handler()
    {
        WindowsMessageBinder.MouseInputTracker.isQuickSave = true;
        // start tracking
        WindowsMessageBinder.MouseInputTracker.StartTracking();
        OnPropertyChanged("IsTrackingStatusText");
    }

    //
    // [Alt + Shift + C] recording with manual save - dialog popup will ask user for file name
    //
    private void _ALT_SHIFT_C_handler()
    {
        WindowsMessageBinder.MouseInputTracker.isQuickSave = false;
        // start tracking
        WindowsMessageBinder.MouseInputTracker.StartTracking();
        OnPropertyChanged("IsTrackingStatusText");
    }

    //
    // [Alt + V] Run snapshot 
    //
    private void _ALT_V_handler()
    {
        _runShapshot();
    }

    //
    // [Alt + L] Toggle looping 
    //
    private void _ALT_L_handler()
    {
        IsLooping = !IsLooping;
    }
}
