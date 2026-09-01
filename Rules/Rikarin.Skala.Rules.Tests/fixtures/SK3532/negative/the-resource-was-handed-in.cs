// The lifetime belongs to whoever built the `Lease`. Disposing it here would close something the
// caller is still holding.

public ref struct Lease {
    public int Size;

    public void Dispose() {
        Size = 0;
    }
}

public ref struct View {
    Lease lease;

    public View(Lease lease) {
        this.lease = lease;
    }

    public int Size => lease.Size;
}
