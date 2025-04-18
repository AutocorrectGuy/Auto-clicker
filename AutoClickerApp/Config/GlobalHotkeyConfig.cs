

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
