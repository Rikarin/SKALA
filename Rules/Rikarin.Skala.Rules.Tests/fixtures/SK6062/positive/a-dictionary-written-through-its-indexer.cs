using System.Collections.Generic;

public static class Indexing {
    public static int Build(IReadOnlyList<string> names) {
        var byName = new Dictionary<string, int>();

        for (var i = 0; i < names.Count; i++) {
            byName[names[i]] = i;
        }

        return names.Count;
    }
}
