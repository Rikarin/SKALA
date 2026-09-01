using System;
using System.Collections.Concurrent;

public sealed class Report {
    // ⚠ `Keys` is a locked snapshot and iterating the table itself is a live walk, so the two loops
    // see different things under concurrent writes. SK4033 reports this receiver's costs instead.
    public static void Write(ConcurrentDictionary<string, int> totals) {
        foreach (var key in totals.Keys) {
            Console.WriteLine(totals[key]);
        }
    }
}
