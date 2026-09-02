using System.Collections.Generic;

public static class Sequences {
    public static IReadOnlyList<T> ToList<T>(this IReadOnlyList<T> source) => source;
}

public sealed class Feed {
    readonly List<string> entries = new();

    // ⚠ Not `System.Linq.Enumerable`'s method. A rule that read the identifier would report this
    // one, which copies nothing at all.
    public IReadOnlyList<string> Items => entries.ToList();
}
