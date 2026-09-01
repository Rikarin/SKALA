using System.Collections.Generic;

public static class Separate {
    public static bool Has(Dictionary<string, int> lookup, string key) {
        int found;

        return lookup.TryGetValue(key, out found);
    }
}
