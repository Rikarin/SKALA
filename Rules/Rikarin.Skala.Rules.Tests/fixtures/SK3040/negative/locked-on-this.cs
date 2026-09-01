public sealed class Counter {
    int value;

    public void Increment() {
        // `lock (this)` is its own smell and is not this rule: the receiver is not a
        // synchronization primitive, so nothing here is two mechanisms confused for one.
        lock (this) {
            value++;
        }
    }
}
