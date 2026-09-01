using System.Collections.Generic;

public sealed class Cache {
    // On every other dictionary `Keys` is a view and `Count` is a field read. SK1034 speaks here.
    public static int Size(Dictionary<string, int> entries) => entries.Keys.Count;
}
