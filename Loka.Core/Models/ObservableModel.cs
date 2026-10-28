using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Loka.Core.Models;

public static class ThreadDispatcher
{
    public static Action<Action>? DispatcherAction { get; set; }

    public static void RunOnUI(Action action)
    {
        if (DispatcherAction != null)
        {
            DispatcherAction(action);
        }
        else
        {
            action();
        }
    }
}

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var handler = PropertyChanged;
        if (handler == null) return;

        ThreadDispatcher.RunOnUI(() =>
        {
            handler(this, new PropertyChangedEventArgs(propertyName));
        });
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
