public sealed class Counter {
    volatile int served;

    public Counter(int seed) {
        served = seed;

        // No other thread can hold a reference to this instance yet, so there is nothing to race.
        served++;
    }

    public int Served => served;
}
