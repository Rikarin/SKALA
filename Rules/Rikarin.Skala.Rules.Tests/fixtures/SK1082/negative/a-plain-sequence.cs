using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // No indexer to reach for; ElementAt is doing the walking it exists to do.
    public static int Third(IEnumerable<int> entries) => entries.ElementAt(2);
}
