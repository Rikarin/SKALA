public sealed class Counter {
    volatile int served;

    public void Serve() {
        // A read, an add and a write. Two threads read the same value and one update is lost.
        served++;
    }

    public int Served => served;
}
