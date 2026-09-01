using System;
using System.IO;

public sealed class Slot : IDisposable {
    readonly MemoryStream? buffer = new();

    public long Length => buffer is null ? 0 : buffer.Length;

    public void Dispose() {
    }
}
