using System.Collections.Generic;
using System.Linq;

public static class Sorted {
    public static IEnumerable<int> Plain(IEnumerable<int> values) =>
        from value in values
        orderby value
        select value;
}
