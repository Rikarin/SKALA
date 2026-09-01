public sealed class Registry {
    readonly object reads = new();

    readonly object writes = new();

    int count;

    public void Add() {
        lock (writes) {
            count++;
        }
    }

    public int Read() {
        lock (reads) {
            return count;
        }
    }

    // Two gates is a hierarchy, and which one was meant to guard `count` is not decidable from the
    // shape — so neither is whether this write is missing one.
    public void Reset() => count = 0;
}
