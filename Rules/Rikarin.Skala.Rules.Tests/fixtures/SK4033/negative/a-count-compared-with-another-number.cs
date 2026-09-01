using System.Collections.Concurrent;

public sealed class Cache {
    // `> 3` is a claim about how many, which `IsEmpty` cannot answer.
    public static bool Busy(ConcurrentDictionary<string, int> entries) => entries.Count > 3;
}
