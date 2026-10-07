using System.Collections.Generic;
using System.Linq;

// Both lambdas read only their parameter and cannot throw, and `ToList` binds the same method on either
// type, so filtering first gives the same list for less sorting.
public static class Probe {
    public static string Run() {
        var values = new List<int> { 5, 3, 9, 1, 7, 3 };
        var kept = values.OrderBy(value => value % 4).Where(value => value > 2).ToList();
        return string.Join(",", kept);
    }
}
