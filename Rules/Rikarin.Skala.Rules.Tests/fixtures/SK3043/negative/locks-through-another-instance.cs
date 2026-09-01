public sealed class Account {
    readonly object balance = new();

    readonly object history = new();

    Account? peer;

    int amount;

    public void Push() {
        lock (balance) {
            lock (peer!.history) {
                amount++;
            }
        }
    }

    public void Pull() {
        // ⚠ **A documented miss, not a false positive avoided.** This is the classic bank-transfer
        // deadlock and it is real: one thread holding `a.balance` and wanting `b.history` against
        // another holding `b.history` and wanting `a.balance`. The rule stays silent because it
        // accepts only a bare identifier or `this.field` as a lock target — the finding it produces
        // names fields and not objects, so "`history` while holding `balance`" would be an
        // ambiguous sentence here about which account each name meant. Widening the rule needs an
        // answer to that question, and this fixture is where the absence of one is recorded.
        lock (history) {
            lock (peer!.balance) {
                amount--;
            }
        }
    }
}
