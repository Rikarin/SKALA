using System.Collections.Generic;
using System.Diagnostics;

public sealed class Tracker {
    readonly HashSet<int> pending = [];

    public void Complete(int id) {
        // Removed in every debug run and in no release run.
        Debug.Assert(pending.Remove(id));
    }
}
