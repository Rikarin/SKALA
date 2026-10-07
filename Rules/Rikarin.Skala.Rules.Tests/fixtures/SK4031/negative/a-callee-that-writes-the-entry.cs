using System.Collections.Generic;
using System.Text;

// ⚠ #412's audit: `Bump` writes the entry after the loop reached it and before the lookup. The
// indexer reads 100 and 200; a deconstructed value would still be 1 and 2.
public static class Probe {
    static void Bump(Dictionary<string, int> totals, string key) => totals[key] = totals[key] * 100;

    public static string Run() {
        var totals = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var written = new StringBuilder();
        foreach (var key in totals.Keys) {
            Bump(totals, key);
            written.Append(key + "=" + totals[key] + ";");
        }

        return written.ToString();
    }
}
