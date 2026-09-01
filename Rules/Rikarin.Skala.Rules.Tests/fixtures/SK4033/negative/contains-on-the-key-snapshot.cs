using System;
using System.Collections.Concurrent;

public sealed class Cache {
    // ⚠ The snapshot's `Contains` uses EqualityComparer<string>.Default; `ContainsKey` uses the
    // comparer the table was constructed with, which is not the same test here.
    public static bool Knows(ConcurrentDictionary<string, int> entries, string key) =>
        entries.Keys.Contains(key);

    public static ConcurrentDictionary<string, int> Insensitive() => new(StringComparer.OrdinalIgnoreCase);
}
