// #400: `Reset()` binds to the partial method's definition, and the write is in its implementation.
// They are one method, and the type calls it while holding the lock.
public sealed partial class Registry {
    readonly object gate = new();

    int count;

    public void Add() {
        lock (gate) {
            Reset();
        }
    }

    public int Read() {
        lock (gate) {
            return count;
        }
    }

    public void Bump() {
        lock (gate) {
            count++;
        }
    }

    public partial void Reset();

    public partial void Reset() => count = 0;
}
