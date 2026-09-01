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
        // ⚠ `the-tenth-write.cs`, and the only difference is this `lock`. Everything else about the
        // two files is identical, which makes the pair the rule's real test.
        lock (gate) {
            count = 0;
        }
    }
}
