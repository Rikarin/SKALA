using System.Threading;

public sealed class Snapshot {
    readonly ReaderWriterLockSlim guard = new();

    int[] items = [];

    public int Sum() {
        // A `try` is present, the release is spelled correctly, and it sits one line outside the
        // block that needed it. Any throw the `catch` does not name walks straight past
        // `ExitReadLock` — which is the case the `try` was written for in the first place.
        guard.EnterReadLock();
        var total = 0;
        try {
            foreach (var item in items) {
                total = checked(total + item);
            }
        } catch (System.OverflowException) {
            total = -1;
        }

        guard.ExitReadLock();

        return total;
    }
}
