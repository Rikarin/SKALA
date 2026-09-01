using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    public static IEnumerable<string> Recent(List<string> entries) =>
        entries.OrderByDescending(entry => entry, StringComparer.Ordinal).Where(entry => entry.Length > 0);
}
