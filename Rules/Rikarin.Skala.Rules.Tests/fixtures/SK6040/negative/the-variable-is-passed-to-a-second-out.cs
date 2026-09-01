using System.Collections.Generic;

public static class Chained {
    public static bool Both(Dictionary<string, int> first, Dictionary<string, int> second, string key) {
        first.TryGetValue(key, out var slot);

        return second.TryGetValue(key, out slot);
    }
}
