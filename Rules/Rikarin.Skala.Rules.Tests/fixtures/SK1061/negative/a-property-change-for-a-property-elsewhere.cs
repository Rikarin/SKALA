using System.ComponentModel;

// The name belongs to another object's surface. `nameof` written here would not resolve it.
public sealed class Proxy : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Forward() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Volume"));
}
