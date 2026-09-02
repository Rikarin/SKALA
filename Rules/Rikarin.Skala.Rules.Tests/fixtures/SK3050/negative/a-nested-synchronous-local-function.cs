using System;
using System.Threading.Tasks;

// The nearest owner of the throw is `Boom`, which is neither `async` nor the method's own body.
public sealed class Panel {
    public async void Refresh() {
        await Task.Yield();

        void Boom() => throw new InvalidOperationException("caller's problem");

        Boom();
    }
}
