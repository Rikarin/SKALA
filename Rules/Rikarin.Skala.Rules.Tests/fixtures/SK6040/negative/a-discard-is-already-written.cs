using System.Collections.Generic;

public static class Probe {
    public static bool Has(Dictionary<string, int> lookup, string key) => lookup.TryGetValue(key, out _);
}
