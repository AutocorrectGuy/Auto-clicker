using System.Diagnostics;

public class AutoClicker : ViewModelBase
{
  private bool _isClicking;
  public bool IsClicking
  {
    get { return _isClicking; }
    set
    {
      _isClicking = value;
      OnPropertyChanged(); // update bool data trigger in ui
      OnPropertyChanged("IsClickingStatusText"); // update text field value in ui
    }
  }

  private int _intervalMs;
  public int IntervalMs
  {
    get { return _intervalMs; }
    private set
    {
      _intervalMs = value;
      OnPropertyChanged(); // update the clicks count number in ui
    }
  }

  private int _clicksCount;
  public int ClicksCount
  {
    get { return _clicksCount; }
    set
    {
      _clicksCount = value;
      OnPropertyChanged();
    }
  }

  public string IsClickingStatusText
  {
    get { return _isClicking ? "Clicking" : "Not clicking"; }
  }

  // CPS input field (binds to user TextBox)
  private string _cpsInput = "40";
  public string CpsInput
  {
    get { return _cpsInput; }
    set
    {
      _cpsInput = value;
      OnPropertyChanged(); // update inputfield value in ui
      UpdateIntervalFromCps(value); // update interval value in ui
    }
  }

  private void UpdateIntervalFromCps(string input)
  {
    double cps;
    if (double.TryParse(
      input,
      System.Globalization.NumberStyles.Float,
      System.Globalization.CultureInfo.InvariantCulture,
      out cps))
    {
      // 1 to 1000 cps
      if (cps >= 0.1 && cps <= 1000)
        IntervalMs = (int)(1000.0 / cps);
    }
  }

  private readonly UserInputHandler _userInputHandler;

  public AutoClicker(UserInputHandler userInputHandler)
  {
    _userInputHandler = userInputHandler;
    _isClicking = false;
    _clicksCount = 0;
    _intervalMs = 25; // default ~40 CPS (1000 ms / 25 ms = 40 cps)
  }

  public void StartClicking()
  {
    // prevent overlapping loops and using more than 1 separate thread 
    if (_isClicking)
      return;

    IsClicking = true;

    // run task on separate thread, so the app does not freeze or bug out
    Task.Run(() =>
    {
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      long intervalTicks = TimeSpan.FromMilliseconds(_intervalMs).Ticks;
      long nextTick = stopwatch.ElapsedTicks;

      // some delt time stuff - better than just using Thread.Sleep(ms)
      while (_isClicking)
      {
        long now = stopwatch.ElapsedTicks;

        if (now >= nextTick)
        {
          _userInputHandler.SendLeftClick();
          nextTick += intervalTicks;
          ClicksCount++;
        }

        Thread.Sleep(1); // small sleep to avoid CPU burn
      }

      stopwatch.Stop();
    });
  }


  public void StopClicking()
  {
    IsClicking = false;
  }
}
