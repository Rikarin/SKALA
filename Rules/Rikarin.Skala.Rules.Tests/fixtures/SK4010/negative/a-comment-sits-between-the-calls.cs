using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static int Ready(List<int> values) =>
        values
            .Where(value => value > 0)
            // the order matters here, and the reader has to know it
            .First();
}
