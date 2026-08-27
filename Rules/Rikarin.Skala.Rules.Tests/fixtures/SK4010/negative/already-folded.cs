using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static int Ready(List<int> values) => values.First(value => value > 0);
}
