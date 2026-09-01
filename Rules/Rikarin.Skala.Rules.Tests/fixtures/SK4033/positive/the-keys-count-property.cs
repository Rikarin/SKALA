using System.Collections.Concurrent;

public sealed class Cache {
    public static int Size(ConcurrentDictionary<string, int> entries) => entries.Keys.Count;
}
