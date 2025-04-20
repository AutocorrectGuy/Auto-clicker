using System;
using System.Windows.Input;

public class RelayCommand : ICommand
{
  private Action _execute;
  private Func<bool> _canExecute;

  public event EventHandler CanExecuteChanged
  {
    add { CommandManager.RequerySuggested += value; }
    remove { CommandManager.RequerySuggested -= value; }
  }

  public RelayCommand(Action execute, Func<bool> canExecute = null)
  {
    _execute = execute;
    _canExecute = canExecute ?? (() => true);
  }

  public void Execute(object param = null)
  {
    _execute();
  }

  public bool CanExecute(object param = null)
  {
    return _canExecute();
  }
}

