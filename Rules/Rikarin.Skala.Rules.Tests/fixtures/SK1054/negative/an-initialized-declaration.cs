using System.Collections.Generic;

// The initializer is a value somebody chose; deleting the declaration deletes it.
public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public int Get(string key) {
        int value = -1;
        if (entries.TryGetValue(key, out value)) {
            return value;
        }

        return 0;
    }
}
