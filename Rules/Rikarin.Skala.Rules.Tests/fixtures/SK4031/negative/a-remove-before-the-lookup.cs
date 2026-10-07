using System.Collections.Generic;

// ⚠ #412's audit: removing the entry during enumeration does not throw since .NET Core 3.0, and the
// lookup after it throws KeyNotFoundException; a deconstructed value would have been read already.
public static class Probe {
    public static int Run() {
        var totals = new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 };
        var sum = 0;
        foreach (var key in totals.Keys) {
            totals.Remove(key);
            sum += totals[key];
        }

        return sum;
    }
}
