// ⚠ `ContainsKey(source.Key)` and the indexer read `Key` twice; `TryGetValue` reads it once.
// Measured for #412's audit with a counting getter: KeyNotFoundException before, a value after.
// A property with a body is not storage.
using System.Collections.Generic;

public sealed class Source {
    int reads;

    public int Key {
        get {
            reads++;
            return reads;
        }
    }
}

public static class Lookup {
    public static string Find(Dictionary<int, string> entries, Source source) {
        if (entries.ContainsKey(source.Key)) {
            var value = entries[source.Key];
            return value;
        }

        return "";
    }
}
