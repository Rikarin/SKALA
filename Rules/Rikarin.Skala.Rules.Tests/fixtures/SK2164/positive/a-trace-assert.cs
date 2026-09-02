using System.Collections.Generic;
using System.Diagnostics;

public sealed class Tracker {
    readonly List<int> items = [];

    public void Drop(int id) {
        Trace.Assert(items.Remove(id));
    }
}
