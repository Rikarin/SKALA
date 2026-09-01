public sealed class Ledger {
    readonly object accounts = new();

    readonly object journal = new();

    int balance;

    int entries;

    public void Post() {
        lock (accounts) {
            lock (journal) {
                balance++;
                entries++;
            }
        }
    }

    public void Reconcile() {
        // ⚠ `two-methods-in-opposite-orders.cs`, and the only difference is these two lines being
        // the right way round. Everything else about the two files is identical, which is what
        // makes this pair the rule's real test rather than its documentation.
        lock (accounts) {
            lock (journal) {
                entries--;
                balance--;
            }
        }
    }
}
