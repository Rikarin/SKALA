using System.Collections.Generic;

public static class Reader {
    public static int Get(Dictionary<string, int> lookup, string key) =>
        lookup.TryGetValue(key, out var found) ? found : 0;
}
