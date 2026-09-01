using System.Collections.Generic;

public sealed class Cache {
    // The static type is what decides; through the interface there is no `IsEmpty` to reach.
    public static int Size(IDictionary<string, int> entries) => entries.Keys.Count;
}
