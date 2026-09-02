using System;
using System.Threading.Tasks;

public sealed class SavedEventArgs : EventArgs { }

public delegate void SavedHandler(object sender, SavedEventArgs e);

public sealed class Editor {
    public void Wire() {
        SavedHandler handler = async (sender, e) => await Task.Yield();

        handler(this, new SavedEventArgs());
    }
}
