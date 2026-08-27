using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool Ready(IEnumerable<int> values) =>
        values
            .Select(value => value * 2)
            .Where(value => value > 0)
            .Any();
}
