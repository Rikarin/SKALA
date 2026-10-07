using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

// ⚠ #412's audit: `LastOrDefault(p)` on an `IList<T>` that is not a `List<T>` scans backward and stops
// at the first match. The predicate logs, so it saw "1,2,3,4" through `Where` and would see "4".
public static class Probe {
    public static string Run() {
        var seen = new List<int>();
        var numbers = new ReadOnlyCollection<int>(new[] { 1, 2, 3, 4 });
        var lastEven = numbers.Where(n => {
                seen.Add(n);
                return n % 2 == 0;
            }
        )
            .LastOrDefault();
        return lastEven + " visited " + string.Join(",", seen);
    }
}
