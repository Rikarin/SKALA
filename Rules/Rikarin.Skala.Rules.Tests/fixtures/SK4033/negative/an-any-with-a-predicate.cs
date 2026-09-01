using System.Collections.Concurrent;
using System.Linq;

public sealed class Cache {
    public static bool AnyLong(ConcurrentDictionary<string, int> entries) =>
        entries.Keys.Any(key => key.Length > 8);
}
