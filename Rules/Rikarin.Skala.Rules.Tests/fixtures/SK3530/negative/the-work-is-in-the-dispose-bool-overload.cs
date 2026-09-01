// The documented pattern. `Dispose()` never touches the field; `Dispose(bool)` does, and reading
// only the entry point would report every faithful implementation of `CA1063`.

using System;
using System.IO;

public class Journal : IDisposable {
    readonly MemoryStream buffer = new();

    public long Length => buffer.Length;

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            buffer.Dispose();
        }
    }
}
