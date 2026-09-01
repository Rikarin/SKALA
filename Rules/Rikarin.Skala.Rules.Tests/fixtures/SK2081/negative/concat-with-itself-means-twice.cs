using System.Collections.Generic;
using System.Linq;

public sealed class Ring {
    // Doubling a sequence is the ordinary reason to write this, so `Concat` is not in the table.
    public static IEnumerable<int> Twice(List<int> items) => items.Concat(items);
}
