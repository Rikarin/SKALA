// `Dispose(bool)` without `base.Dispose(disposing)` is the same shape and belongs to `CA2215`, which
// ships in the box. This rule is the asynchronous counterpart nothing else has.

using System;
using System.IO;

public class Journal : IDisposable {
    readonly MemoryStream stream = new();

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            stream.Dispose();
        }
    }
}

public sealed class Rolling : Journal {
    readonly MemoryStream extra = new();

    protected override void Dispose(bool disposing) {
        extra.Dispose();
    }
}
