using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    // ⚠ SK4010 offers an overlapping edit here and keeps it: folding the predicate into `First`
    // lets the search stop at the first match, and once applied this shape no longer exists.
    public static int Recent(List<int> entries) =>
        entries.OrderBy(entry => entry).Where(entry => entry > 0).First();
}
