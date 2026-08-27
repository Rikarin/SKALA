using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static int Ready(IEnumerable<int> values) => values.Where(value => value > 0).Count();
}
