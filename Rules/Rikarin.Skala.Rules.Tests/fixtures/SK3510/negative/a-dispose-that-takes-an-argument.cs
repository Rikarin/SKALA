using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }

    public void Dispose(bool flushFirst) { }
}

public sealed class Consumer {
    // The overload with an argument is not the contract the `using` invokes, and deleting it would
    // drop the flush.
    public void Report() {
        using var handle = new Handle();
        handle.Dispose(true);
    }
}
