using System;
using System.IO;

public sealed class Session : IDisposable {
    readonly MemoryStream buffer = new();

    public long Length => buffer.Length;

    public void Dispose() {
        Shutdown();
    }

    void Shutdown() {
        buffer.Close();
    }
}
