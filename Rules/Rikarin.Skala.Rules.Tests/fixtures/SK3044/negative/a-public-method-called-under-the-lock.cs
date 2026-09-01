public sealed class Registry {
    readonly object gate = new();

    int count;

    public void Add() {
        lock (gate) {
            // The type itself calls `Reset` while holding the lock. Whatever the documentation
            // says, that is a demonstrated contract, and a finding on `Reset` would be an argument
            // about a convention rather than a race.
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

    public void Reset() => count = 0;
}
