using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // SK4010's shape, not this one: `First` is one of the nine operators that take the predicate
    // directly, and the two consumer sets are disjoint by construction.
    public static object First(IEnumerable<object> values) => values.Where(value => value is string).First();
}
