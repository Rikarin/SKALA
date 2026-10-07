// ⚠ #430: `Find` reads the live size, so it reaches an element the predicate appended; `FirstOrDefault`
// walks the span it took before the first call. Measured `null` before the fix and `z` after.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    public static string Run() {
        var names = new List<string> { "a", "b" };
        var added = false;
        var found = names.FirstOrDefault(s => {
                if (!added) {
                    added = true;
                    names.Add("z");
                }

                return s == "z";
            }
        );
        return found ?? "null";
    }
}
