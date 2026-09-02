public sealed class Counter {
    readonly object gate = new object();

    int value;

    public void Increment() {
        // ⚠ The pin on "`SK1023` and `SK3061` are disjoint by construction". `SK1023` modernizes
        // exactly this — a private `readonly object` field used only as a lock target — to
        // `System.Threading.Lock`, and shape 2 here requires a field that is *not* `readonly`. No
        // field can carry both findings, which is why neither rule declares `supersedes`.
        //
        // ⚠ It also records a gate that cannot be sabotage-tested, and that was measured rather than
        // assumed: deleting the analyzer's `IsReadOnly` check leaves the whole fixture suite green
        // — 3 014 fixture cases, nothing red — because the only write to a `readonly` field
        // the compiler permits is in a constructor or an initializer, and the "assigned outside a
        // constructor" gate already declines both. The `readonly` check is redundant with C#'s own
        // rule rather than with nothing — kept because it states the intent at the top of the walk,
        // not because a fixture holds it.
        lock (gate) {
            value++;
        }
    }
}
