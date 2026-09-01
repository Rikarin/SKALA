public sealed class Counter {
    readonly object gate = new();

    int value;

    public void Increment() {
        lock (gate) {
            value++;
        }
    }

    public int Read() {
        lock (gate) {
            return value;
        }
    }
}
