using System.Threading;

public sealed class Registry {
    readonly ReaderWriterLockSlim guard = new();

    int generation;

    public void Bump() {
        // ⚠ The `try`/`finally` is present and it is still wrong, which is why the rule pairs per
        // enter method rather than per type: `EnterWriteLock` has no matching `ExitWriteLock` here.
        // `ExitReadLock` on a write lock throws `SynchronizationLockException`, so the write lock is
        // held forever *and* the finally block throws over whatever was propagating.
        guard.EnterWriteLock();
        try {
            generation++;
        } finally {
            guard.ExitReadLock();
        }
    }
}
