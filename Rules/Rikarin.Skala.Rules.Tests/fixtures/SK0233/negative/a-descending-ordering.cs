using System.Collections.Generic;
using System.Linq;

public static class Sorted {
    public static IEnumerable<int> Descending(IEnumerable<int> values) =>
        from value in values
        orderby value descending
        select value;
}
