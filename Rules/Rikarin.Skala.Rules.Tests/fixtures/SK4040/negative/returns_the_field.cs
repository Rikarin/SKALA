using System.Collections.Generic;

public sealed class Feed {
    readonly List<string> entries = new();

    public IReadOnlyList<string> Items => entries;
}
