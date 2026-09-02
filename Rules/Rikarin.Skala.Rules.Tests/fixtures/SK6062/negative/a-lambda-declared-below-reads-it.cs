using System;
using System.Collections.Generic;

// The scan covers the whole member rather than running forward from the declaration, so a reader
// written after the writes is still seen.
public static class Captured {
    public static Func<int> Run(IEnumerable<string> items) {
        var kept = new List<string>();
        foreach (var item in items) {
            kept.Add(item);
        }

        return () => kept.Count;
    }
}
