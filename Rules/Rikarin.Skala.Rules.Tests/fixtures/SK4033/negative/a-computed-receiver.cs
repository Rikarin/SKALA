using System.Collections.Concurrent;

public sealed class Cache {
    // The fix writes the receiver a second time, so it may not be something that runs.
    public static int Size() => Entries().Keys.Count;

    static ConcurrentDictionary<string, int> Entries() => new();
}
