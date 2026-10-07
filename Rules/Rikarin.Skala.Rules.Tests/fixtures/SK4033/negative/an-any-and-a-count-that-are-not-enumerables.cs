// ⚠ #424: `Any()` and `Count()` were matched by name. A project's own `Any<T>(this ICollection<T>)`
// beats `Enumerable.Any` on `Keys`, whose static type is `ICollection<T>`, so the call is not the
// whole-table copy the rule describes and `IsEmpty` is not what it computes (#412's audit:
// "none 10" became "any 1").
using System.Collections.Concurrent;
using System.Collections.Generic;

public static class CollectionExtensions {
    public static bool Any<T>(this ICollection<T> source) => source.Count > 1;

    public static int Count<T>(this ICollection<T> source) => source.Count * 10;
}

public static class Probe {
    public static string Run() {
        var d = new ConcurrentDictionary<string, int>();
        d["a"] = 1;
        return (d.Keys.Any() ? "any" : "none") + " " + d.Keys.Count();
    }
}
