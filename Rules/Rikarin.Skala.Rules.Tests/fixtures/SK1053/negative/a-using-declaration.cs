using System;

// The name is not the point of a `using` declaration; the scope is.
public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Cache {
    public void Warm() {
        using var handle = new Handle();
    }
}
