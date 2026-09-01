using System.Threading;

public sealed class Queue {
    readonly SemaphoreSlim slots = new(4);

    int depth;

    public void Push() {
        // The monitor taken here excludes nobody who is waiting on `slots` itself.
        lock (slots) {
            depth++;
        }
    }
}
