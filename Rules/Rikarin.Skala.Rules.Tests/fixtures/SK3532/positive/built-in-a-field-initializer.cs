public ref struct Handle {
    public int Slot;

    public void Dispose() {
        Slot = -1;
    }
}

public ref struct Worker {
    Handle handle = new();

    public Worker() {
    }

    public int Slot => handle.Slot;
}
