using System;

public sealed class Handle : IDisposable {
    public int Value => 42;

    public void Dispose() { }
}

public sealed class Consumer {
    public int Report() {
        using var handle = new Handle();
        var value = handle.Value;
        handle.Dispose();
        return value;
    }
}
