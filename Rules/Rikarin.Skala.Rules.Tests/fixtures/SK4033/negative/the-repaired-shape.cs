using System.Collections.Concurrent;

public sealed class Cache {
    public static int Size(ConcurrentDictionary<string, int> entries) => entries.Count;

    public static bool Empty(ConcurrentDictionary<string, int> entries) => entries.IsEmpty;

    public static bool Populated(ConcurrentDictionary<string, int> entries) => !entries.IsEmpty;
}
