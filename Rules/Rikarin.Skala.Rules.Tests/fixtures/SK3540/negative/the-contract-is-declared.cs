using System;
using System.IO;

public sealed class Journal : IDisposable {
    readonly MemoryStream file = new();

    public void Dispose() {
        file.Dispose();
    }
}

// An explicit implementation is still an implementation, and `AllInterfaces` sees it.
public sealed class Ledger : IDisposable {
    readonly MemoryStream file = new();

    void IDisposable.Dispose() {
        file.Dispose();
    }
}
