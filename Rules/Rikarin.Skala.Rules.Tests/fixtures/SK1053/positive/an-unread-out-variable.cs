using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public bool Has(string key) => entries.TryGetValue(key, out var value);
}
