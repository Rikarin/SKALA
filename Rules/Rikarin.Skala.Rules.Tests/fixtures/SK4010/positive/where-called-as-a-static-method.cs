using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static int Ready(List<int> values) => Enumerable.Where(values, value => value > 0).First();
}
