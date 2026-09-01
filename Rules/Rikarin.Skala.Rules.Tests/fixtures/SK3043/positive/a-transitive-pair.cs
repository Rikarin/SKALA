public sealed class Pipeline {
    readonly object input = new();

    readonly object middle = new();

    readonly object output = new();

    int count;

    public void Drain() {
        lock (input) {
            lock (middle) {
                // `input` before `output`, with `middle` in between. The pair is still an order.
                lock (output) {
                    count++;
                }
            }
        }
    }

    public void Flush() {
        lock (output) {
            lock (input) {
                count--;
            }
        }
    }
}
