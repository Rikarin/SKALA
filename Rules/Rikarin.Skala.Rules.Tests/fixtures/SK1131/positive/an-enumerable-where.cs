using System.Collections.Generic;
using System.Linq;

// ⚠ `System.Linq` is imported and `Queryable.Where` is therefore in scope — but not applicable to an
// array, so the member group holds `Enumerable.Where` alone and nothing could bind to a tree.
public static class Filtering {
    public static IEnumerable<int> Positive(int[] values) {
        return values.Where(delegate(int v) { return v > 0; });
    }
}
