using System;
using System.Threading.Tasks;

// ⚠ `(object, TEventArgs) -> void` is the shape the language gives events, and an `async` handler
// subscribed to one is the sanctioned use of `async void`. There is no other signature available.
public sealed class Editor {
    public event EventHandler? Saved;

    public void Wire() {
        Saved += async (sender, e) => await Task.Yield();
    }
}
