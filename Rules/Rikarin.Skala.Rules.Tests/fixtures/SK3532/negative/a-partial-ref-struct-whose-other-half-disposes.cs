public ref struct Lease {
    public int Size;

    public void Dispose() {
        Size = 0;
    }
}

public ref partial struct Session {
    Lease lease;

    public Session(int size) {
        lease = new Lease();
        lease.Size = size;
    }
}

public ref partial struct Session {
    public void Dispose() {
        lease.Dispose();
    }
}
