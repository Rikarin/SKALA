using System.Collections.Generic;

public sealed class Sync {
    public static bool Covers(SortedSet<int> ports) => ports.IsProperSubsetOf(ports);
}
