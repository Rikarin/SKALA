using System.Collections.Generic;

// ⚠ `_` is a name here, so `_ = entries.Remove(key);` would assign to the parameter instead of
// discarding — silently, and with the same shape.
public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public string Drop(string _, string key) {
        var removed = entries.Remove(key);
        return _;
    }
}
