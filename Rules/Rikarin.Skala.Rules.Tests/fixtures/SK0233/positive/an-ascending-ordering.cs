using System.Collections.Generic;
using System.Linq;

public static class Sorted {
    public static IEnumerable<int> Ascending(IEnumerable<int> values) =>
        from value in values
        orderby value ascending
        select value;
}
