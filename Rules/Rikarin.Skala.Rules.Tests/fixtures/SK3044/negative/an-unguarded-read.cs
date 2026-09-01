public sealed class Registry {
    readonly object gate = new();

    int count;

    public void Add() {
        lock (gate) {
            count++;
        }
    }

    public void Remove() {
        lock (gate) {
            count--;
        }
    }

    // A real hazard, and also exactly the shape of a deliberate best-effort snapshot. The two are
    // not distinguishable here, so the rule requires the unguarded access to be a write.
    public int Approximate => count;
}
