public sealed class Cache {
    readonly object sharedGate = new object();

    int hits;

    public void Touch() {
        // ⚠ The single most important negative in the set, and the reason the object-creation
        // initializer is the load-bearing clause of shape 1. This is a local, it is locked exactly
        // like the bad shape, and it aliases one object that every call and every thread reaches —
        // the code is correct and the rule must stay silent. Only a *creation* counts.
        var gate = sharedGate;

        lock (gate) {
            hits++;
        }
    }
}
