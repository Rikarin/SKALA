using System;
using System.IO;

public class Node : IDisposable {
    protected readonly MemoryStream buffer = new();

    public long Length => buffer.Length;

    public void Dispose() {
    }
}
