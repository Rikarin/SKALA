using System.Collections.Generic;

public static class Named {
    public static string Describe(Dictionary<string, int> lookup, string key) {
        lookup.TryGetValue(key, out var slot);

        return nameof(slot);
    }
}
