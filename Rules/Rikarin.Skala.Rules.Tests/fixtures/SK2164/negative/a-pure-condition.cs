using System.Collections.Generic;
using System.Diagnostics;

public sealed class Tracker {
    readonly List<int> items = [];

    public void Check() {
        Debug.Assert(items.Count > 0);
        Debug.Assert(items.Contains(1));
        Debug.Assert(items is not null, "items must exist");
    }
}
