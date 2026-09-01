using System;
using System.Collections.Generic;

public sealed class Report {
    // The loop wants the keys and only the keys, which is what `Keys` is for.
    public static void Write(Dictionary<string, int> totals) {
        foreach (var key in totals.Keys) {
            Console.WriteLine(key);
        }
    }
}
