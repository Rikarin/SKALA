using System.Threading;

public sealed class Cache {
    readonly ReaderWriterLockSlim guard = new();

    int generation;

    public int Touch() {
        // The third `ReaderWriterLockSlim` pairing, and the one whose enter and release names are
        // longest — the table has to carry it explicitly, because there is no rule of thumb that
        // turns `EnterUpgradeableReadLock` into `ExitUpgradeableReadLock` and `TryEnter` into `Exit`.
        guard.EnterUpgradeableReadLock();
        try {
            return generation;
        } finally {
            guard.ExitUpgradeableReadLock();
        }
    }
}
