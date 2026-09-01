using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    // ⚠ The two calls swap and the comment does not, so it would end up describing the other one.
    public static IEnumerable<int> Recent(List<int> entries) =>
        entries.OrderBy(entry => entry) // ties keep their source order
            .Where(entry => entry > 0);
}
