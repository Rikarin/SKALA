public sealed class Registry {
    readonly object gate = new();

    int count;

    public void Add() {
        lock (gate) {
            AddCore();
        }
    }

    public int Read() {
        lock (gate) {
            return count;
        }
    }

    public void Reset() {
        lock (gate) {
            count = 0;
        }
    }

    // ⚠ The "caller holds the lock" contract, and the most common shape this rule would otherwise
    // wreck. A private helper is never reported: it cannot be entered except through a member of
    // this type, and the rule has no standing to argue about which of those took the lock.
    void AddCore() => count++;
}
