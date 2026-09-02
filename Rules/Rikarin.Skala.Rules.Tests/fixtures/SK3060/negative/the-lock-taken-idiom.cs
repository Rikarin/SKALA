using System.Threading;

public sealed class Ledger {
    readonly object gate = new();

    int balance;

    public void Post(int amount) {
        // The form the framework documents, and the one the compiler itself emits for `lock` since
        // C# 4: the `Enter` is *inside* the `try` and the `finally` releases only if it succeeded.
        // The enter sits under the `try` rather than before it, so a rule that looked for "a `try`
        // that follows the enter" would report this — the rule asks where the release is instead.
        var lockTaken = false;
        try {
            Monitor.Enter(gate, ref lockTaken);
            balance = checked(balance + amount);
        } finally {
            if (lockTaken) {
                Monitor.Exit(gate);
            }
        }
    }
}
