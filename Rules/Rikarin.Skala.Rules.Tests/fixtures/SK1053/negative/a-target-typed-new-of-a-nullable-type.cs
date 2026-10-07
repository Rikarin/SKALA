// `Lease? lease = new();` — the declared type cannot simply be moved into the creation: `new T?()`
// is CS8628 for a reference type, and for a value type `new()` against `T?` builds the underlying
// `T`, which `new T?()` does not. The rule declines both rather than choosing a spelling.
public struct Lease {
    public Lease() { }
}

public sealed class Pool {
    public void Warm() {
        Lease? lease = new();
    }
}
