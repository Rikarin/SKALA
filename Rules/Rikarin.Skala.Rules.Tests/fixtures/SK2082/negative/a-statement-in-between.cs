using System;
using System.Collections.Generic;

public sealed class Limits {
    // Anything that is not an element write to the same collection ends the run. `Log` could hold a
    // reference to `limits` and read the first value; the analyzer has no way to know that it does
    // not.
    public static void Configure(Dictionary<string, int> limits) {
        limits["read"] = 100;
        Console.WriteLine("configured");
        limits["read"] = 25;
    }
}
