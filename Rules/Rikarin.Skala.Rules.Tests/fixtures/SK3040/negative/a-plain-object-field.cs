public sealed class Counter {
    readonly object gate = new();

    int value;

    public void Increment() {
        // The ordinary shape. `SK1023` may want `System.Threading.Lock` here; this rule never
        // fires on `object`, which is what keeps the two disjoint.
        lock (gate) {
            value++;
        }
    }
}
