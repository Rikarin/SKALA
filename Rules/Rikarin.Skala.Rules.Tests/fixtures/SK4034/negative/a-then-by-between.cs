using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    // The `Where` receiver is the `ThenBy`, so this is not the shape the swap is defined for.
    public static IEnumerable<string> Recent(List<string> entries) =>
        entries.OrderBy(entry => entry.Length).ThenBy(entry => entry).Where(entry => entry.Length > 0);
}
