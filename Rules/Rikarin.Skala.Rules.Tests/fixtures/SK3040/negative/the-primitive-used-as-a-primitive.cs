using System.Threading;

public sealed class Queue {
    readonly SemaphoreSlim slots = new(4);

    int depth;

    public void Push() {
        slots.Wait();
        try {
            depth++;
        } finally {
            slots.Release();
        }
    }
}
