public sealed class Meter {
    int count;

    public void Bump() {
        // The shape the rule exists for. Every call allocates its own object and therefore its own
        // monitor, so this critical section excludes nobody and the whole construct is a no-op.
        var gate = new object();

        lock (gate) {
            count++;
        }
    }
}
