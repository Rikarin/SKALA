using System.Collections.Generic;

public sealed class Cache {
    // On every other dictionary `Keys` is a view and `Count` is a field read, so there is nothing
    // expensive to report. SK1034 used to speak here; it is retired (#281) and nothing does now.
    public static int Size(Dictionary<string, int> entries) => entries.Keys.Count;
}
