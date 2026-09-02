using System.Threading;

public sealed class Ledger {
    readonly object gate = new();

    int balance;

    public void Post(int amount) {
        // The shape the rule exists for: the release is written, and it is on the happy path. The
        // `checked` add throws on overflow, `Exit` never runs, and every later caller blocks forever
        // in a place that has nothing to do with the throw.
        Monitor.Enter(gate);
        balance = checked(balance + amount);
        Monitor.Exit(gate);
    }
}
