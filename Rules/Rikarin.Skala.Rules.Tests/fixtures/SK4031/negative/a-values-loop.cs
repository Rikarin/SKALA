using System;
using System.Collections.Generic;

public sealed class Report {
    public static void Write(Dictionary<string, int> totals) {
        foreach (var total in totals.Values) {
            Console.WriteLine(total);
        }
    }
}
