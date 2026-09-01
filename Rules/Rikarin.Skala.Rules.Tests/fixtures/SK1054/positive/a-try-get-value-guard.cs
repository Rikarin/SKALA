using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public int Get(string key) {
        int value;
        if (entries.TryGetValue(key, out value)) {
            return value;
        }

        return 0;
    }
}
