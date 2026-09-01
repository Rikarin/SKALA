using System;
using System.Collections.Generic;

public sealed class Report {
    public static void Write(Dictionary<string, int> totals, string wanted) {
        foreach (var key in totals.Keys) {
            Console.WriteLine(key + ": " + totals[wanted]);
        }
    }
}
