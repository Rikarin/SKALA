// ⚠ `Dispose(bool)` is not the pattern. `using` binds to a public parameterless `Dispose()` and to
// nothing else, so this type reads as disposable and is not.

public ref struct Lease {
    public int Size;

    public void Dispose() {
        Size = 0;
    }
}

public ref struct Scope {
    Lease lease;

    public Scope(int size) {
        lease = new Lease();
        lease.Size = size;
    }

    public void Dispose(bool flush) {
        if (flush) {
            lease.Dispose();
        }
    }
}
