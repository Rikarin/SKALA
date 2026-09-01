using System.Collections.Concurrent;
using System.Linq;

public sealed class Cache {
    public static int Size(ConcurrentDictionary<string, int> entries) => entries.Values.Count();
}
