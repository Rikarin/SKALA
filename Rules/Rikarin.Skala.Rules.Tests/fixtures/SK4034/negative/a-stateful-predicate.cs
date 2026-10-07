using System.Collections.Generic;
using System.Linq;

// ⚠ #412's audit: the predicate counts what it has seen, so the order it sees elements in decides
// which survive — "1,3" sorted first, "3,5" filtered first.
public static class Probe {
    public static string Run() {
        var values = new List<int> { 5, 3, 9, 1, 7 };
        var taken = 0;
        var cheapest = values.OrderBy(value => value).Where(value => taken++ < 2).ToList();
        return string.Join(",", cheapest);
    }
}
