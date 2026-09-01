public sealed class Counter {
    int count;

    public void Advance(int step) {
        count = count + step;
    }
}
