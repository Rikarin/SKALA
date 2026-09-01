using System.Collections.Generic;

public sealed class Limits {
    // ⚠ `other` may hold "read". "Not the same key" is not "a different key", so the run ends here
    // rather than stepping over a write that may already have replaced the first value.
    public static void Configure(Dictionary<string, int> limits, string other) {
        limits["read"] = 100;
        limits[other] = 50;
        limits["read"] = 25;
    }
}
