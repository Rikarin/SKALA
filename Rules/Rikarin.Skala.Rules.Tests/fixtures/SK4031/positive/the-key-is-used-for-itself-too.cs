using System;
using System.Collections.Generic;

public sealed class Report {
    public static void Write(Dictionary<string, int> totals, List<string> order) {
        foreach (var key in totals.Keys) {
            if (order.Contains(key)) {
                Console.WriteLine(totals[key]);
            }
        }
    }
}
