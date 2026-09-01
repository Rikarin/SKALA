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

    public int Read() {
        lock (gate) {
            return count;
        }
    }

    public void Reset() {
        // The path added last, and the one nobody looked at twice.
        count = 0;
    }
}
