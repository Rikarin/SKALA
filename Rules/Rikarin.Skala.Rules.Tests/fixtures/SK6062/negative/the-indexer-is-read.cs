using System.Collections.Generic;

public static class IndexerRead {
    public static int Run(IReadOnlyList<string> names) {
        var byName = new Dictionary<string, int>();
        for (var i = 0; i < names.Count; i++) {
            byName[names[i]] = i;
        }

        return byName["first"];
    }
}
