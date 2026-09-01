public sealed class Ledger {
    readonly object accounts = new();

    readonly object journal = new();

    int balance;

    int entries;

    public void Post() {
        lock (accounts) {
            balance++;
        }

        lock (journal) {
            entries++;
        }
    }

    public void Reconcile() {
        // Neither lock is ever held while the other is taken, so there is no order to be
        // inconsistent about and nothing to deadlock on.
        lock (journal) {
            entries--;
        }

        lock (accounts) {
            balance--;
        }
    }
}
