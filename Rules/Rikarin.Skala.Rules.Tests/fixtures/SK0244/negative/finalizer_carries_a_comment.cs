sealed class Buffer {
    // Kept deliberately: the profiler run in #412 depends on the queue entry.
    ~Buffer() { }

    public int Length => 0;
}
