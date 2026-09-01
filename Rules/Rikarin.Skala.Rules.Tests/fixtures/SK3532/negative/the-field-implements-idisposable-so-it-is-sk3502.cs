// ⚠ The disjointness fixture, and it is the whole reason the interface test exists. C# 13 lets a
// `ref struct` implement `IDisposable`, and there the ownership is one `SK3502` can see and does
// report. Removing the test in `RefStructOwnedDisposableAnalyzer` makes both rules speak about this
// one field, at the one span, and `supersedes` would then suppress the wrong one of the two.

using System;

public ref struct Lease : IDisposable {
    public int Size;

    public void Dispose() {
        Size = 0;
    }
}

public ref struct Session {
    Lease lease = new();

    public Session() {
    }

    public int Size => lease.Size;
}
