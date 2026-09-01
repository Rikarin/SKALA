using System.Threading;

public sealed class Queue {
    readonly SemaphoreSlim slots = new(4);

    int depth;

    public void Push() {
        // ⚠ The stated scope limit, recorded rather than hidden: this is the same mistake spelled
        // without the keyword, and the rule matches the `lock` statement only. Widening it to
        // `Monitor.Enter` means proving the matching `Exit` is on every path, which is a different
        // analysis; until that exists the rule declines rather than guesses.
        Monitor.Enter(slots);
        try {
            depth++;
        } finally {
            Monitor.Exit(slots);
        }
    }
}
