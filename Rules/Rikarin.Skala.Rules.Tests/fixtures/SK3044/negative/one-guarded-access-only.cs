public sealed class Registry {
    readonly object gate = new();

    int count;

    public void Add() {
        lock (gate) {
            count++;
        }
    }

    // One guarded access and one bare one is as likely to be a lock introduced in the wrong place
    // as a lock forgotten in the other, and the rule has no way to say which. Two is the floor.
    public void Reset() => count = 0;
}
