public ref struct Lease {
    public int Size;

    public Lease(int size) {
        Size = size;
    }

    public void Dispose() {
        Size = 0;
    }
}

public ref struct Session {
    Lease lease;

    public Session(int size) {
        lease = new Lease(size);
    }

    public int Size => lease.Size;
}
