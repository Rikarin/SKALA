public sealed class Meter {
    volatile int total;

    public void Record(int sample) {
        total += sample;
    }

    public int Total => total;
}
