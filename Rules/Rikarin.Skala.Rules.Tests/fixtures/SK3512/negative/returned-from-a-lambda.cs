using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Factory {
    // ⚠ The `return` belongs to the lambda, not to `Take`, and when the delegate runs is not
    // something the enclosing `using` says anything about.
    public Func<Handle> Take() {
        using var handle = new Handle();
        return () => handle;
    }
}
