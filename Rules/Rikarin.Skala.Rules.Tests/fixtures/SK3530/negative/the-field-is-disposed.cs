using System;
using System.IO;

public sealed class Store : IDisposable {
    readonly MemoryStream buffer = new();

    public long Length => buffer.Length;

    public void Dispose() {
        buffer.Dispose();
    }
}
