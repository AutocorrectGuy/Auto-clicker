﻿using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string name = "")
    {
        if (PropertyChanged == null) return;
        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
    }
}