using System;
using System.IO;

public sealed partial class Split : IDisposable {
    readonly MemoryStream buffer = new();

    public long Length => buffer.Length;
}

public sealed partial class Split {
    public void Dispose() {
        buffer.Dispose();
    }
}
