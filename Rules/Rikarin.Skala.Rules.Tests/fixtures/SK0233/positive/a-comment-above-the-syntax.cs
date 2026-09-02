using System.Collections.Generic;
using System.Linq;

public static class Filtering {
    /// <summary>A documentation comment on the declaration.</summary>
    public static IEnumerable<int> Positive(IEnumerable<int> values) =>
        // And an ordinary comment on the line above the finding. The span the fix deletes is the
        // three characters `(v)`, and nothing on this line is inside it.
        values.Where((v) => v > 0);
}
