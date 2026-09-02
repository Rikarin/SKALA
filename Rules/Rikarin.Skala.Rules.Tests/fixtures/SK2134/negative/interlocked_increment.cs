using System.Threading;

// ⚠ Declined by construction rather than by a filter: this is an argument, not an assignment, so
// there is no assignment node for the rule to visit. It is also the repair for the counter fixture
// in the positive set, which is why it must stay silent.
sealed class Widget {
    static int created;

    public Widget() {
        Interlocked.Increment(ref created);
    }

    public static int Created => Volatile.Read(ref created);
}
