using System.Collections.Generic;

// Without `[Conditional]` the call site is never deleted, so the effect always happens and there is no
// difference between builds to report.
public sealed class Tracker {
    readonly HashSet<int> pending = [];

    static void Require(bool condition) { }

    public void Complete(int id) {
        Require(pending.Remove(id));
    }
}
