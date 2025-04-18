using System.Windows;

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