using System.Collections.Generic;
using System.Text;

// The dictionary is created here and handed nowhere, so nothing the loop calls can change an entry
// between reaching it and reading it.
public static class Probe {
    public static string Run() {
        var totals = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var written = new StringBuilder();
        foreach (var key in totals.Keys) {
            written.Append(key + ": " + totals[key] + ";");
        }

        return written.ToString();
    }
}
