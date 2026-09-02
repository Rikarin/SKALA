using System.Collections.Generic;
using System.Linq;

public sealed class Pages {
    readonly List<string> entries = new();

    // ⚠ An indexer's brackets are a call and carry no promise of cheapness.
    public IReadOnlyList<string> this[int index] => entries.ToList();
}
