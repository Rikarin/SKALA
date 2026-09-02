using System.Threading;

public sealed class Registry {
    readonly ReaderWriterLockSlim guard = new();

    int generation;

    public void Bump() {
        // The correct spelling of the mismatched-release positive one directory over. The pairing is
        // per enter method, so the only thing that changed is `Read` to `Write`.
        guard.EnterWriteLock();
        try {
            generation = checked(generation + 1);
        } finally {
            guard.ExitWriteLock();
        }
    }
}
