public sealed class Counter {
    readonly object gate = new();

    volatile int served;

    public void Serve() {
        // The monitor already makes the read-modify-write atomic. `volatile` beside it is
        // redundant, not wrong, and a finding here would send a reader to correct threading code.
        lock (gate) {
            served++;
        }
    }

    public int Served => served;
}
