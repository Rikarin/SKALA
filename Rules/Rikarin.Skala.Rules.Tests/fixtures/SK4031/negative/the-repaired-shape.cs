using System;
using System.Collections.Generic;

public sealed class Report {
    public static void Write(Dictionary<string, int> totals) {
        foreach (var (key, value) in totals) {
            Console.WriteLine(key + ": " + value);
        }
    }
}
