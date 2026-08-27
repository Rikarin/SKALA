using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static List<int> Ready(List<int> values) =>
        values.Where(value => value > 0).Select(value => value * 2).ToList();
}
