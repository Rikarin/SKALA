using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    // ⚠ After the sort the index is a position in sorted order; before it, in source order.
    public static IEnumerable<int> Recent(List<int> entries) =>
        entries.OrderBy(entry => entry).Where((entry, index) => index < 10);
}
