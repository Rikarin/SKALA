using System.ComponentModel;

// The empty name is the convention for "every property changed", not a name at all.
public sealed class Bulk : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; set; } = string.Empty;

    public void Reset() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
}
