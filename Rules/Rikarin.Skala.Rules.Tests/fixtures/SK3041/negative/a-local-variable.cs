public sealed class Counter {
    volatile int served;

    public int Snapshot() {
        var local = served;
        local++;
        return local;
    }
}
