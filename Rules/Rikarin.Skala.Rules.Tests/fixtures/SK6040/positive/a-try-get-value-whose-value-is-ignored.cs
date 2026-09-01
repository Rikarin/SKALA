using System.Collections.Generic;

public static class Cache {
    public static void Seed(Dictionary<string, int> lookup, string key, int value) {
        if (!lookup.TryGetValue(key, out var existing)) {
            lookup.Add(key, value);
        }
    }
}
