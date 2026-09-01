using System.Collections.Concurrent;
using System.Linq;

public sealed class Cache {
    // `long` where `Count` is `int`; narrowing the static type can move an overload.
    public static long Size(ConcurrentDictionary<string, int> entries) => entries.Keys.LongCount();
}
