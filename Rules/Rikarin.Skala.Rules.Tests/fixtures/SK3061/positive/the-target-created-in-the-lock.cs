public sealed class Ledger {
    int entries;

    public void Add() {
        // The degenerate form of the same mistake, with the local removed. It compiles, it reads as
        // a critical section, and the monitor it takes has never been seen by another thread.
        lock (new object()) {
            entries++;
        }
    }
}
