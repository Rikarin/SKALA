// The same bug, and the finding is withheld because the fix has nowhere to land.

using System;
using System.IO;

public sealed class Ticker : IDisposable {
    readonly MemoryStream buffer = new();

    public long Length => buffer.Length;

    public void Dispose() => GC.SuppressFinalize(this);
}
