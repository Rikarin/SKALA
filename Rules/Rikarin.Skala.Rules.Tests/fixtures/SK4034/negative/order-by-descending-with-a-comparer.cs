using System;
using System.Collections.Generic;
using System.Linq;

// ⚠ #425: a comparer is code somebody wrote, and moving the filter changes which elements it is
// asked to compare. Declined, as a key selector that is not inert is.
public sealed class Feed {
    public static IEnumerable<string> Recent(List<string> entries) =>
        entries.OrderByDescending(entry => entry, StringComparer.Ordinal).Where(entry => entry.Length > 0);
}
