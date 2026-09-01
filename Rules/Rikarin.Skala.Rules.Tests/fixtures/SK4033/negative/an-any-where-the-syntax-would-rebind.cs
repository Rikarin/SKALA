using System.Collections.Concurrent;
using System.Linq;

public sealed class Cache {
    // ⚠ `!entries.IsEmpty.ToString()` parses, binds, and means something else.
    public static string Describe(ConcurrentDictionary<string, int> entries) => entries.Keys.Any().ToString();
}
