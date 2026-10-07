using System.Collections.Generic;
using System.Linq;

// ⚠ #412's audit: reordered, the argument is an `IOrderedEnumerable<int>`, and the overload that takes
// one is the better match. The call answered "plain" and would answer "ordered".
public static class Probe {
    static string Describe(IEnumerable<int> values) => "plain";

    static string Describe(IOrderedEnumerable<int> values) => "ordered";

    public static string Run() {
        var values = new List<int> { 3, 1, 2 };
        return Describe(values.OrderBy(value => value).Where(value => value > 1));
    }
}
