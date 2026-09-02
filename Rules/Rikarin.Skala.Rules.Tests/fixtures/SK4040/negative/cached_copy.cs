using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    readonly List<string> entries = new();
    IReadOnlyList<string>? cached;

    // ⚠ One copy, not one per read. Declined by construction rather than by a filter: the getter's
    // expression is a coalescing assignment and not a materializer.
    public IReadOnlyList<string> Items => cached ??= entries.ToList();
}
