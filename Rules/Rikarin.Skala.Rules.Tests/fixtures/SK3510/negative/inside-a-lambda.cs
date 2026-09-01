using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ When the delegate runs is not something the enclosing `using` says anything about, so the
    // rule withdraws rather than reasoning about it.
    public Action Report() {
        using var handle = new Handle();
        return () => handle.Dispose();
    }
}
