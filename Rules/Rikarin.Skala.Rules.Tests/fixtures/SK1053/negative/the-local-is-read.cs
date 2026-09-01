using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public bool Drop(string key) {
        var removed = entries.Remove(key);
        return removed;
    }
}
