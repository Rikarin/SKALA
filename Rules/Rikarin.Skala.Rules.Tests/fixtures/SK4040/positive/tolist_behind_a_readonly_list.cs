using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    readonly List<string> entries = new();

    public IReadOnlyList<string> Items => entries.ToList();
}
