using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool Ready(List<int> values) => values.Where(value => value > 0).All(value => value < 10);
}
