using System.Collections.Generic;

// A discard has no type, so replacing an explicitly typed out-variable can change which overload
// the call resolves to.
public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public bool Has(string key) => entries.TryGetValue(key, out int value);
}
