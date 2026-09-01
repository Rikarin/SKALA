public sealed class Registry {
    readonly object gate = new();

    int count;

    public Registry(int seed) {
        // No other thread can hold a reference to this instance yet, so there is nothing to guard
        // against. Constructors, finalizers and field initializers are not accesses for this rule.
        count = seed;
    }

    public void Add() {
        lock (gate) {
            count++;
        }
    }

    public int Read() {
        lock (gate) {
            return count;
        }
    }
}
