using System;
using System.Collections.Generic;

public static class Deferred {
    public static Func<int> Get(Dictionary<string, int> lookup, string key) {
        lookup.TryGetValue(key, out var found);

        return () => found;
    }
}
