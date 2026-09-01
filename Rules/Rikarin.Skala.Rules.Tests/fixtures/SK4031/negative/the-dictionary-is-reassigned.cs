using System;
using System.Collections.Generic;

public sealed class Report {
    public static void Write(Dictionary<string, int> totals) {
        var current = totals;
        foreach (var key in current.Keys) {
            Console.WriteLine(current[key]);
            current = totals;
        }
    }
}
