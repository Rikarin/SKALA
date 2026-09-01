public sealed class Counter {
    readonly object gate = new();

    int value;

    public void Increment() {
        // A monitor is recursive for the thread that holds it, so this is redundant and not an
        // order. `(gate, gate)` is not a pair.
        lock (gate) {
            lock (gate) {
                value++;
            }
        }
    }
}
