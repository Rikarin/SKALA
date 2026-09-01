using System;
using System.Collections.Generic;

public sealed class Report {
    // `IDictionary<TKey, TValue>` makes no promise that `Keys` and the dictionary agree on order.
    public static void Write(IDictionary<string, int> totals) {
        foreach (var key in totals.Keys) {
            Console.WriteLine(totals[key]);
        }
    }
}
