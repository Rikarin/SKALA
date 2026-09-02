using System;
using System.IO;

public sealed class Owned {
    public void Write() {
        using var sink = (IDisposable)new StringWriter();
        GC.KeepAlive(sink);
    }
}
