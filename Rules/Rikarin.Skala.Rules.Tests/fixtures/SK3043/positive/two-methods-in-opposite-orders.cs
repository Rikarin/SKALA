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
        // ⚠ The other half. Each method reads as an ordinary pair of nested locks; only the two
        // together deadlock, and they are almost never on the same screen.
        lock (journal) {
            lock (accounts) {
                entries--;
                balance--;
            }
        }
    }
}
