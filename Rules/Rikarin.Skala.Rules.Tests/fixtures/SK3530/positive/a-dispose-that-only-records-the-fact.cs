using System;
using System.IO;

public sealed class Store : IDisposable {
    readonly FileStream stream = new("data.bin", FileMode.OpenOrCreate);

    bool closed;

    public long Length => stream.Length;

    public bool IsClosed() => closed;

    public void Dispose() {
        closed = true;
    }
}
