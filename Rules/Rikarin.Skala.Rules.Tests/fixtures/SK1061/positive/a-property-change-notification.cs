using System.ComponentModel;

public sealed class Model : INotifyPropertyChanged {
    string title = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title {
        get => title;
        set {
            title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Title"));
        }
    }
}
