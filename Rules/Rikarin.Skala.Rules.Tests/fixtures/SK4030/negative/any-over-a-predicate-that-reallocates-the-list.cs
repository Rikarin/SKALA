// ⚠ #430: `Exists` is not the safe one of the three. It fixes its end once, as `Any` does, but reads
// `_items` afresh on every step — so after a predicate grows the list past its capacity it reads the
// new array, where `Any` keeps reading the span over the old one. Measured `1,2,3` before the fix and
// `1,7,3` after.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    public static string Run() {
        var list = new List<int>(3) { 1, 2, 3 };
        var seen = new List<int>();
        list.Any(n => {
                seen.Add(n);
                if (seen.Count == 1) {
                    list.Add(9);
                    list[1] = 7;
                }

                return false;
            }
        );
        return string.Join(",", seen);
    }
}
