using System;

public sealed class Store {
    [Obsolete("")]
    public void Save() { }

    [Obsolete("   ")]
    public void Flush() { }

    [Obsolete(null)]
    public void Close() { }
}
