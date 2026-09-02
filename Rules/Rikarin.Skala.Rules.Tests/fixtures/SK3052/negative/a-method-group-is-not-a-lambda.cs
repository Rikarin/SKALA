using System;
using System.Threading.Tasks;

// An `async void` method converted to `Action` is the same hazard by a different route, and the
// declaration is where it is named — this rule is about the conversion a lambda hides.
public sealed class Wiring {
    public void Wire() {
        Action callback = Refresh;

        callback();
    }

    async void Refresh() => await Task.Yield();
}
