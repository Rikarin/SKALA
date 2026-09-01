public sealed class Gate {
    volatile bool closed;

    public void Close() {
        // A single write. This is exactly what `volatile` is for and there is no read to race.
        closed = true;
    }

    public bool Closed => closed;
}
