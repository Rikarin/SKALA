using System;
using System.IO;

public sealed class Pair : IDisposable {
    readonly MemoryStream first = new();

    readonly MemoryStream second = new();

    public long Total => first.Length + second.Length;

    public void Dispose() {
        first.Dispose();
    }
}
