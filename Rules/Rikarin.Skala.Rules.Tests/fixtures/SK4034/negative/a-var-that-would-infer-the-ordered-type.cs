using System.Collections.Generic;
using System.Linq;

// The local is declared `var`, so it would be typed `IOrderedEnumerable<int>` after the swap, and the
// call that follows binds to whatever takes that type.
public static class Probe {
    static string Describe(IEnumerable<int> values) => "plain";

    static string Describe(IOrderedEnumerable<int> values) => "ordered";

    public static string Run() {
        var values = new List<int> { 3, 1, 2 };
        var kept = values.OrderBy(value => value).Where(value => value > 1);
        return Describe(kept);
    }
}
