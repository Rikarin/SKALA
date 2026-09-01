using System.Collections.Concurrent;

public sealed class Limits {
    // Another thread may read between the two writes. "Nothing read it" is not a claim this
    // analysis is entitled to make on this type.
    public static void Configure(ConcurrentDictionary<string, int> limits) {
        limits["read"] = 100;
        limits["read"] = 25;
    }
}
