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