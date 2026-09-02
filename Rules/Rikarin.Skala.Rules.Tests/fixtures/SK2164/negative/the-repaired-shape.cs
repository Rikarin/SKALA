using System.Collections.Generic;
using System.Diagnostics;

// The repair the finding asks for: the effect is hoisted out, so it happens in every build and the
// assertion still checks it in the ones that keep it.
public sealed class Tracker {
    readonly HashSet<int> pending = [];

    public void Complete(int id) {
        var removed = pending.Remove(id);
        Debug.Assert(removed);
    }
}
