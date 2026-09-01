public struct Lease {
    public int Size;

    public void Dispose() {
        Size = 0;
    }
}

public struct Session {
    Lease lease;

    public Session(int size) {
        lease = new Lease();
        lease.Size = size;
    }

    public int Size => lease.Size;
}
