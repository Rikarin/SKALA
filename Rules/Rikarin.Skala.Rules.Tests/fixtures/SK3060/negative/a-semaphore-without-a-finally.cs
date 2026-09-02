using System.Threading;

public sealed class Throttle {
    readonly SemaphoreSlim slots = new(4);

    int served;

    public void Serve() {
        // ⚠ The stated scope limit, recorded rather than hidden. This is the same *shape* as the
        // first positive fixture and a different *fact*: a semaphore is a counter, not an owned
        // lock, and acquiring it in one method and releasing it in another — or on one path and not
        // another — is how a semaphore is normally used. `Mutex.WaitOne`/`ReleaseMutex` is out for
        // the same reason. Widening the table to those means guessing at intent, so the rule
        // declines rather than guesses.
        slots.Wait();
        served = checked(served + 1);
        slots.Release();
    }
}
