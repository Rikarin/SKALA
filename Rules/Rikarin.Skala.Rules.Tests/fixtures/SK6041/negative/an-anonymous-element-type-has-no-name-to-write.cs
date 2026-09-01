using System.Collections.Generic;
using System.Linq;

public static class Projected {
    public static int TotalLength(List<string> names) {
        var total = 0;
        foreach (object row in names.Select(static name => new { name.Length })) {
            total += row.GetHashCode();
        }

        return total;
    }
}
