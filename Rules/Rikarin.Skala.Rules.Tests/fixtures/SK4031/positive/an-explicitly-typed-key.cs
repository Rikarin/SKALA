using System;
using System.Collections.Generic;

public sealed class Report {
    public void Write() {
        var totals = new SortedDictionary<string, int> { ["a"] = 1 };
        foreach (string key in totals.Keys) {
            Console.WriteLine(totals[key] + totals[key]);
        }
    }
}
