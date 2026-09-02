using System.Threading;

public sealed class Ledger {
    readonly object gate = new();

    int balance;

    public void Post(int amount) {
        // The hand-written form of the keyword, and the whole point of the rule: the release is in a
        // `finally`, so the lock comes back however the critical section leaves.
        Monitor.Enter(gate);
        try {
            balance = checked(balance + amount);
        } finally {
            Monitor.Exit(gate);
        }
    }
}
