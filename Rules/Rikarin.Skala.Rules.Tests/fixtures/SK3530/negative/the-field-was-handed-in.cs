using System;
using System.IO;

public sealed class Reader : IDisposable {
    readonly Stream source;

    public Reader(Stream source) => this.source = source;

    public long Length => source.Length;

    public void Dispose() {
    }
}
