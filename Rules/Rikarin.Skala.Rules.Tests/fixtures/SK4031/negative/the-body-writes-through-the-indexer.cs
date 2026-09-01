using System.Collections.Generic;

public sealed class Report {
    // ⚠ The deconstructed value would be a copy, so assigning to it stops updating the dictionary.
    public static void Bump(Dictionary<string, int> totals) {
        foreach (var key in totals.Keys) {
            totals[key] = totals[key] + 1;
        }
    }
}
