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