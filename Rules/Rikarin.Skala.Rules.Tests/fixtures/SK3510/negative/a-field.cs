using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Consumer : IDisposable {
    readonly Handle handle = new();

    // A field is not owned by any `using`; this is the only disposal it gets.
    public void Dispose() {
        handle.Dispose();
    }
}
