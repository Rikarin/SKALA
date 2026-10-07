using System;
using System.Collections.Generic;

public sealed class Report {
    public static void Write(List<string> order) {
        var totals = new Dictionary<string, int>();
        totals["a"] = 1;
        foreach (var key in totals.Keys) {
            if (order.Contains(key) && totals.ContainsKey(key)) {
                Console.WriteLine(totals[key]);
            }
        }
    }
}
