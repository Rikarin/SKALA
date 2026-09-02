using System.Threading;

public sealed class Pool {
    int taken;

    public void Take() {
        // ⚠ A fresh local *and* a lock over a synchronization primitive, and `SK3040` already
        // reports the second reading — which is the one that tells the reader what to do, because
        // the repair is to use the semaphore rather than to hoist it into a field. Declined here so
        // the two rules never both fire on one `lock`. Removing the primitive exclusion from the
        // analyzer turns this fixture red.
        var slots = new SemaphoreSlim(1);

        lock (slots) {
            taken++;
        }
    }
}
