using System;
using System.Collections.Generic;

public sealed class Report {
    readonly SortedDictionary<string, int> totals = new();

    public void Write() {
        foreach (string key in this.totals.Keys) {
            Console.WriteLine(this.totals[key] + this.totals[key]);
        }
    }
}
