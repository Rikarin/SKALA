using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public void Drop(string key) {
        var removed = entries.Remove(key);
    }
}
