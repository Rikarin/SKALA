using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public int Get(string key) => entries.TryGetValue(key, out int value) ? value : 0;
}
